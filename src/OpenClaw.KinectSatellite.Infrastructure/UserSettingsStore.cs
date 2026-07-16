using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Win32;
using OpenClaw.KinectSatellite.Core;

namespace OpenClaw.KinectSatellite.Infrastructure;

public sealed class UserSettingsStore : IUserSettingsStore
{
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("OpenClaw.KinectSatellite.v1");
    private readonly string _directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "OpenClaw", "KinectSatellite");
    private string SettingsPath => Path.Combine(_directory, "settings.json");
    private string TokenPath => Path.Combine(_directory, "token.dat");
    public SatelliteSettings Current { get; private set; }

    public UserSettingsStore() => Current = Load();

    public async Task SaveAsync(SatelliteSettings settings, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(_directory);
        var publicSettings = settings with { HomeAssistant = CopyHomeAssistant(settings.HomeAssistant, "") };
        await File.WriteAllTextAsync(SettingsPath, JsonSerializer.Serialize(publicSettings, new JsonSerializerOptions { WriteIndented = true }), cancellationToken);
        var protectedToken = ProtectedData.Protect(Encoding.UTF8.GetBytes(settings.HomeAssistant.AccessToken), Entropy, DataProtectionScope.CurrentUser);
        await File.WriteAllBytesAsync(TokenPath, protectedToken, cancellationToken);
        ConfigureStartup(settings.Application.LaunchAtStartup);
        Current = settings;
    }

    private SatelliteSettings Load()
    {
        SatelliteSettings settings;
        try { settings = JsonSerializer.Deserialize<SatelliteSettings>(File.ReadAllText(SettingsPath)) ?? Defaults(); }
        catch { settings = Defaults(); }
        string token = "";
        try { token = Encoding.UTF8.GetString(ProtectedData.Unprotect(File.ReadAllBytes(TokenPath), Entropy, DataProtectionScope.CurrentUser)); }
        catch (IOException) { }
        catch (CryptographicException) { }
        return settings with { HomeAssistant = CopyHomeAssistant(settings.HomeAssistant, token) };
    }

    private static HomeAssistantOptions CopyHomeAssistant(HomeAssistantOptions source, string token) => new()
    {
        Url = source.Url, AccessToken = token, PipelineId = source.PipelineId, DeviceId = source.DeviceId
    };

    private static SatelliteSettings Defaults() => new(new(), new(), new(), new());

    private static void ConfigureStartup(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run");
        if (enabled) key.SetValue("OpenClaw Kinect Satellite", $"\"{Environment.ProcessPath}\" --minimized");
        else key.DeleteValue("OpenClaw Kinect Satellite", false);
    }
}

