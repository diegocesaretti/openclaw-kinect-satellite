using System.ComponentModel;
using System.Globalization;
using System.Windows;
using Microsoft.Win32;
using OpenClaw.KinectSatellite.Core;
using Forms = System.Windows.Forms;

namespace OpenClaw.KinectSatellite;

public partial class MainWindow : Window
{
    private readonly IUserSettingsStore _settings;
    private readonly ISatelliteController _controller;
    private readonly IHomeAssistantConnectionTester _tester;
    private readonly Forms.NotifyIcon _tray;
    private bool _reallyClose;

    public MainWindow(IUserSettingsStore settings, ISatelliteController controller, IHomeAssistantConnectionTester tester)
    {
        InitializeComponent();
        _settings = settings;
        _controller = controller;
        _tester = tester;
        LoadSettings(settings.Current);
        _controller.StateChanged += (_, _) => Dispatcher.Invoke(UpdateStatus);
        _tray = CreateTrayIcon();
        UpdateStatus();
    }

    private Forms.NotifyIcon CreateTrayIcon()
    {
        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("Open settings", null, (_, _) => ShowWindow());
        menu.Items.Add("Start satellite", null, async (_, _) => await RunActionAsync(() => _controller.StartAsync()));
        menu.Items.Add("Stop satellite", null, async (_, _) => await RunActionAsync(() => _controller.StopAsync()));
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("Exit", null, (_, _) => { _reallyClose = true; System.Windows.Application.Current.Shutdown(); });
        var icon = new Forms.NotifyIcon { Text = "OpenClaw Kinect Satellite", Icon = System.Drawing.SystemIcons.Application, ContextMenuStrip = menu, Visible = true };
        icon.DoubleClick += (_, _) => ShowWindow();
        return icon;
    }

    private void ShowWindow() { Show(); WindowState = WindowState.Normal; Activate(); }

    private void LoadSettings(SatelliteSettings value)
    {
        HaUrl.Text = value.HomeAssistant.Url;
        HaToken.Password = value.HomeAssistant.AccessToken;
        PipelineId.Text = value.HomeAssistant.PipelineId ?? "";
        DeviceId.Text = value.HomeAssistant.DeviceId ?? "";
        ModelPath.Text = value.WakeWord.ModelPath;
        Threshold.Text = value.WakeWord.Threshold.ToString(CultureInfo.InvariantCulture);
        TriggerFrames.Text = value.WakeWord.TriggerFrames.ToString(CultureInfo.InvariantCulture);
        Cooldown.Text = value.WakeWord.CooldownSeconds.ToString(CultureInfo.InvariantCulture);
        EchoCancellation.IsChecked = value.Kinect.EchoCancellation;
        NoiseSuppression.IsChecked = value.Kinect.NoiseSuppression;
        LoggingLevel.Text = value.Application.LoggingLevel;
        LaunchAtStartup.IsChecked = value.Application.LaunchAtStartup;
    }

    private bool TryBuildSettings(out SatelliteSettings settings, out string error)
    {
        settings = default!;
        if (!Uri.TryCreate(HaUrl.Text.Trim(), UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https")) { error = "Home Assistant URL must be an absolute HTTP or HTTPS URL."; return false; }
        if (string.IsNullOrWhiteSpace(HaToken.Password)) { error = "A Home Assistant access token is required."; return false; }
        if (string.IsNullOrWhiteSpace(ModelPath.Text) || !File.Exists(ModelPath.Text.Trim())) { error = "Select an existing MicroWakeWord ONNX model file."; return false; }
        if (!float.TryParse(Threshold.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var threshold) || threshold is < 0 or > 1) { error = "Threshold must be a number from 0 to 1."; return false; }
        if (!int.TryParse(TriggerFrames.Text, out var frames) || frames is < 1 or > 20) { error = "Trigger frames must be an integer from 1 to 20."; return false; }
        if (!int.TryParse(Cooldown.Text, out var cooldown) || cooldown is < 0 or > 30) { error = "Cooldown must be an integer from 0 to 30 seconds."; return false; }
        settings = new SatelliteSettings(
            new KinectOptions { EchoCancellation = EchoCancellation.IsChecked == true, NoiseSuppression = NoiseSuppression.IsChecked == true },
            new WakeWordOptions { ModelPath = ModelPath.Text.Trim(), Threshold = threshold, TriggerFrames = frames, CooldownSeconds = cooldown },
            new HomeAssistantOptions { Url = uri.ToString().TrimEnd('/'), AccessToken = HaToken.Password, PipelineId = NullIfEmpty(PipelineId.Text), DeviceId = NullIfEmpty(DeviceId.Text) },
            new ApplicationOptions { LoggingLevel = LoggingLevel.Text, LaunchAtStartup = LaunchAtStartup.IsChecked == true });
        error = "";
        return true;
    }

    private async void Save_Click(object sender, RoutedEventArgs e)
    {
        if (!TryBuildSettings(out var value, out var error)) { ShowError(error); return; }
        await RunActionAsync(async () =>
        {
            var restart = _controller.State == SatelliteState.Running;
            if (restart) await _controller.StopAsync();
            await _settings.SaveAsync(value);
            if (restart) await _controller.StartAsync();
            FeedbackText.Foreground = System.Windows.Media.Brushes.SeaGreen;
            FeedbackText.Text = "Settings saved securely" + (restart ? " and the satellite was restarted." : ".");
        });
    }

    private async void TestConnection_Click(object sender, RoutedEventArgs e)
    {
        if (!Uri.TryCreate(HaUrl.Text.Trim(), UriKind.Absolute, out _) || string.IsNullOrWhiteSpace(HaToken.Password)) { ShowError("Enter a valid URL and access token before testing."); return; }
        FeedbackText.Text = "Testing connection…";
        var result = await _tester.TestAsync(HaUrl.Text.Trim(), HaToken.Password);
        FeedbackText.Foreground = result.Success ? System.Windows.Media.Brushes.SeaGreen : System.Windows.Media.Brushes.Firebrick;
        FeedbackText.Text = result.Message;
    }

    private async void Start_Click(object sender, RoutedEventArgs e) => await RunActionAsync(() => _controller.StartAsync());
    private async void Stop_Click(object sender, RoutedEventArgs e) => await RunActionAsync(() => _controller.StopAsync());
    private void Browse_Click(object sender, RoutedEventArgs e) { var dialog = new OpenFileDialog { Filter = "ONNX models (*.onnx)|*.onnx|All files (*.*)|*.*" }; if (dialog.ShowDialog() == true) ModelPath.Text = dialog.FileName; }
    private async Task RunActionAsync(Func<Task> action) { try { await action(); } catch (Exception ex) { ShowError(ex.Message); } }
    private void ShowError(string message) { FeedbackText.Foreground = System.Windows.Media.Brushes.Firebrick; FeedbackText.Text = message; }
    private void UpdateStatus() { StatusText.Text = _controller.LastError is null ? _controller.State.ToString() : $"{_controller.State}: {_controller.LastError}"; StartButton.IsEnabled = _controller.State is SatelliteState.Stopped or SatelliteState.Faulted; StopButton.IsEnabled = _controller.State == SatelliteState.Running; _tray.Text = $"OpenClaw - {_controller.State}"; }
    private static string? NullIfEmpty(string value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    protected override void OnClosing(CancelEventArgs e) { if (!_reallyClose) { e.Cancel = true; Hide(); } else _tray.Dispose(); base.OnClosing(e); }
}
