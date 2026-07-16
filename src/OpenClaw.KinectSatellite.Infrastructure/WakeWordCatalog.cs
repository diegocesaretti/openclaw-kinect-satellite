using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using OpenClaw.KinectSatellite.Core;

namespace OpenClaw.KinectSatellite.Infrastructure;

public sealed class WakeWordCatalog : IWakeWordCatalog
{
    private static readonly Uri License = new("https://creativecommons.org/licenses/by-nc-sa/4.0/legalcode.txt");

    public IReadOnlyList<WakeWordModelDefinition> Models { get; } =
    [
        new("alexa", "Alexa", new("https://github.com/dscripka/openWakeWord/releases/download/v0.5.1/alexa_v0.1.onnx"), License, .5f, 2,
            "Alexa model from openWakeWord v0.5.1 by David Scripka and contributors; CC BY-NC-SA 4.0."),
        new("hey_jarvis", "Jarvis", new("https://github.com/dscripka/openWakeWord/releases/download/v0.5.1/hey_jarvis_v0.1.onnx"), License, .5f, 2,
            "Hey Jarvis model from openWakeWord v0.5.1 by David Scripka and contributors; CC BY-NC-SA 4.0.")
    ];

    public WakeWordModelDefinition Get(string id) => Models.FirstOrDefault(x => x.Id == id)
        ?? throw new ArgumentException($"Unknown wake word '{id}'.", nameof(id));
}

/// <summary>Installs the pinned official openWakeWord ONNX feature and classifier models.</summary>
public sealed class WakeWordModelInstaller(IWakeWordCatalog catalog, IHttpClientFactory clients, ILogger<WakeWordModelInstaller> logger) : IWakeWordModelInstaller
{
    private static readonly Uri MelSpectrogram = new("https://github.com/dscripka/openWakeWord/releases/download/v0.5.1/melspectrogram.onnx");
    private static readonly Uri Embedding = new("https://github.com/dscripka/openWakeWord/releases/download/v0.5.1/embedding_model.onnx");
    private readonly string _root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "OpenClaw", "KinectSatellite", "models", "openwakeword-v0.5.1");

    public async Task<InstalledWakeWordModel> EnsureInstalledAsync(string id, CancellationToken cancellationToken = default)
    {
        var definition = catalog.Get(id);
        var directory = Path.Combine(_root, definition.Id);
        var metadataPath = Path.Combine(directory, "metadata.json");
        if (File.Exists(metadataPath))
        {
            var cached = JsonSerializer.Deserialize<InstalledWakeWordModel>(await File.ReadAllTextAsync(metadataPath, cancellationToken));
            if (cached is not null && await IsValidAsync(cached, cancellationToken))
                return cached;
        }

        Directory.CreateDirectory(directory);
        var client = clients.CreateClient(nameof(WakeWordModelInstaller));
        var classifier = await DownloadVerifiedAsync(client, definition.ClassifierUri, directory, cancellationToken);
        var mel = await DownloadVerifiedAsync(client, MelSpectrogram, directory, cancellationToken);
        var embedding = await DownloadVerifiedAsync(client, Embedding, directory, cancellationToken);

        var licensePath = Path.Combine(directory, "LICENSE-CC-BY-NC-SA-4.0.txt");
        await File.WriteAllBytesAsync(licensePath, await client.GetByteArrayAsync(definition.LicenseUri, cancellationToken), cancellationToken);
        await File.WriteAllTextAsync(Path.Combine(directory, "ATTRIBUTION.txt"),
            $"{definition.Attribution}{Environment.NewLine}Classifier: {definition.ClassifierUri}{Environment.NewLine}Classifier SHA-256: {classifier.Hash}{Environment.NewLine}" +
            $"Mel spectrogram: {MelSpectrogram}{Environment.NewLine}Mel SHA-256: {mel.Hash}{Environment.NewLine}" +
            $"Embedding: {Embedding}{Environment.NewLine}Embedding SHA-256: {embedding.Hash}{Environment.NewLine}" +
            $"License: {definition.LicenseUri}{Environment.NewLine}Non-commercial use only; ShareAlike and attribution terms apply.{Environment.NewLine}",
            cancellationToken);

        var installed = new InstalledWakeWordModel(classifier.Path, mel.Path, embedding.Path, classifier.Hash, mel.Hash, embedding.Hash, licensePath);
        await File.WriteAllTextAsync(metadataPath, JsonSerializer.Serialize(installed, new JsonSerializerOptions { WriteIndented = true }), cancellationToken);
        logger.LogInformation("Installed openWakeWord {WakeWord} ONNX bundle; classifier SHA-256 {Sha256}", definition.DisplayName, classifier.Hash);
        return installed;
    }

    private static async Task<(string Path, string Hash)> DownloadVerifiedAsync(HttpClient client, Uri uri, string directory, CancellationToken token)
    {
        byte[] first;
        byte[] second;
        try
        {
            first = await client.GetByteArrayAsync(uri, token);
            second = await client.GetByteArrayAsync(uri, token);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            throw new InvalidOperationException($"Could not download the pinned openWakeWord model asset '{uri}'. Check the internet connection and try again.", ex);
        }

        var firstHash = Convert.ToHexString(SHA256.HashData(first)).ToLowerInvariant();
        var secondHash = Convert.ToHexString(SHA256.HashData(second)).ToLowerInvariant();
        if (!CryptographicOperations.FixedTimeEquals(Convert.FromHexString(firstHash), Convert.FromHexString(secondHash)))
            throw new InvalidDataException($"Repeated downloads of '{uri}' produced different SHA-256 digests; the asset was not installed.");

        var path = Path.Combine(directory, Path.GetFileName(uri.LocalPath));
        await File.WriteAllBytesAsync(path, first, token);
        return (path, firstHash);
    }

    private static async Task<bool> IsValidAsync(InstalledWakeWordModel model, CancellationToken token) =>
        await MatchesAsync(model.ClassifierPath, model.ClassifierSha256, token) &&
        await MatchesAsync(model.MelSpectrogramPath, model.MelSpectrogramSha256, token) &&
        await MatchesAsync(model.EmbeddingPath, model.EmbeddingSha256, token) &&
        File.Exists(model.LicensePath);

    private static async Task<bool> MatchesAsync(string path, string expected, CancellationToken token)
    {
        if (!File.Exists(path)) return false;
        await using var stream = File.OpenRead(path);
        var actual = Convert.ToHexString(await SHA256.HashDataAsync(stream, token)).ToLowerInvariant();
        return string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase);
    }
}
