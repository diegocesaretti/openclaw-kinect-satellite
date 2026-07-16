using OpenClaw.KinectSatellite.Infrastructure;
using OpenClaw.KinectSatellite.Core;
using System.Text.Json;

namespace OpenClaw.KinectSatellite.Tests;

public sealed class WakeWordCatalogTests
{
    [Fact]
    public void ContainsOnlyApprovedUserFacingWakeWords()
    {
        var models = new WakeWordCatalog().Models;

        Assert.Collection(models,
            alexa => { Assert.Equal("alexa", alexa.Id); Assert.Equal("Alexa", alexa.DisplayName); },
            jarvis => { Assert.Equal("hey_jarvis", jarvis.Id); Assert.Equal("Jarvis", jarvis.DisplayName); });
    }

    [Theory]
    [InlineData("alexa")]
    [InlineData("hey_jarvis")]
    public void OfficialCatalogEntriesUsePinnedOnnxAssetsAndValidDefaults(string id)
    {
        var model = new WakeWordCatalog().Get(id);

        Assert.Equal(Uri.UriSchemeHttps, model.ClassifierUri.Scheme);
        Assert.Equal("github.com", model.ClassifierUri.Host);
        Assert.Contains("/openWakeWord/releases/download/v0.5.1/", model.ClassifierUri.AbsolutePath, StringComparison.Ordinal);
        Assert.EndsWith(".onnx", model.ClassifierUri.AbsolutePath, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(Uri.UriSchemeHttps, model.LicenseUri.Scheme);
        Assert.Equal(.5f, model.DefaultThreshold);
        Assert.InRange(model.DefaultTriggerFrames, 1, 20);
        Assert.Contains("CC BY-NC-SA 4.0", model.Attribution, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("alexa")]
    [InlineData("hey_jarvis")]
    public void SelectedWakeWordRoundTripsInPerUserSettingsShape(string id)
    {
        var value = new SatelliteSettings(new(), new WakeWordOptions { WakeWordId = id }, new(), new());

        var restored = JsonSerializer.Deserialize<SatelliteSettings>(JsonSerializer.Serialize(value));

        Assert.NotNull(restored);
        Assert.Equal(id, restored.WakeWord.WakeWordId);
    }
}
