using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using OpenClaw.KinectSatellite.Core;
using OpenClaw.KinectSatellite.Infrastructure;

namespace OpenClaw.KinectSatellite;

public partial class DiagnosticsWindow : Window
{
    private readonly ISatelliteController _controller;
    private readonly string _logDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "OpenClaw", "KinectSatellite", "logs");

    public DiagnosticsWindow(ISatelliteController controller)
    {
        InitializeComponent();
        _controller = controller;
        RefreshDiagnostics();
    }

    private void Refresh_Click(object sender, RoutedEventArgs e) => RefreshDiagnostics();

    private void Copy_Click(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrEmpty(DiagnosticsText.Text))
            Clipboard.SetText(DiagnosticsText.Text);
    }

    private void OpenLogs_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Directory.CreateDirectory(_logDirectory);
            Process.Start(new ProcessStartInfo(_logDirectory) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.ToString(), "Could not open logs folder", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    private void RefreshDiagnostics()
    {
        var report = new StringBuilder();
        report.AppendLine("OpenClaw Kinect Satellite diagnostics");
        report.AppendLine($"Generated: {DateTimeOffset.Now:O}");
        report.AppendLine();

        Append(report, "Satellite state", _controller.State.ToString());
        Append(report, "Last error", _controller.LastError ?? "<none>");
        Append(report, "Process architecture", RuntimeInformation.ProcessArchitecture.ToString());
        Append(report, "64-bit process", Environment.Is64BitProcess.ToString());
        Append(report, "Operating system", RuntimeInformation.OSDescription);
        Append(report, ".NET runtime", RuntimeInformation.FrameworkDescription);
        Append(report, "Application directory", AppContext.BaseDirectory);
        Append(report, "KINECTSDK10_DIR", Environment.GetEnvironmentVariable("KINECTSDK10_DIR") ?? "<not set>");
        Append(report, "Log directory", _logDirectory);

        report.AppendLine();
        report.AppendLine("Microsoft.Kinect.dll search");
        try
        {
            foreach (var candidate in KinectService.GetKinectAssemblyCandidates())
            {
                var exists = File.Exists(candidate);
                report.AppendLine($"  [{(exists ? "FOUND" : "missing")}] {candidate}");
                if (!exists) continue;

                try
                {
                    var assembly = AssemblyName.GetAssemblyName(candidate);
                    var version = FileVersionInfo.GetVersionInfo(candidate);
                    report.AppendLine($"      Assembly: {assembly.FullName}");
                    report.AppendLine($"      File version: {version.FileVersion ?? "<unknown>"}");
                }
                catch (Exception ex)
                {
                    report.AppendLine($"      Could not inspect DLL: {ex.GetType().Name}: {ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            report.AppendLine($"  Probe failed: {ex}");
        }

        report.AppendLine();
        report.AppendLine("Latest log (last 200 lines)");
        try
        {
            var latest = Directory.Exists(_logDirectory)
                ? Directory.EnumerateFiles(_logDirectory, "*.log")
                    .OrderByDescending(File.GetLastWriteTimeUtc)
                    .FirstOrDefault()
                : null;

            if (latest is null)
            {
                report.AppendLine("  <no log file found>");
            }
            else
            {
                report.AppendLine($"File: {latest}");
                report.AppendLine(new string('-', 80));
                foreach (var line in File.ReadLines(latest).TakeLast(200))
                    report.AppendLine(line);
            }
        }
        catch (Exception ex)
        {
            report.AppendLine($"  Could not read logs: {ex}");
        }

        DiagnosticsText.Text = report.ToString();
        DiagnosticsText.ScrollToHome();
    }

    private static void Append(StringBuilder report, string name, string value) =>
        report.AppendLine($"{name}: {value}");
}
