using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using OpenClaw.KinectSatellite.Core;

namespace OpenClaw.KinectSatellite.Infrastructure;

public sealed class WakeWordCatalog : IWakeWordCatalog
{
    private static readonly Uri License = new("https://raw.githubusercontent.com/esphome/micro-wake-word-models/main/LICENSE");
    public IReadOnlyList<WakeWordModelDefinition> Models { get; } =
    [
        new("alexa", "Alexa", new("https://raw.githubusercontent.com/esphome/micro-wake-word-models/main/models/v2/alexa.json"), License, .90f, 5, "Alexa model from ESPHome micro-wake-word-models."),
        new("hey_jarvis", "Jarvis", new("https://raw.githubusercontent.com/esphome/micro-wake-word-models/main/models/v2/hey_jarvis.json"), License, .90f, 5, "Hey Jarvis model from ESPHome micro-wake-word-models.")
    ];

    public WakeWordModelDefinition Get(string id) => Models.FirstOrDefault(x => x.Id == id)
        ?? throw new ArgumentException($"Unknown wake word '{id}'.", nameof(id));
}

/// <summary>Downloads official model manifests and assets without silently substituting or renaming model formats.</summary>
public sealed class WakeWordModelInstaller(IWakeWordCatalog catalog, IHttpClientFactory clients, ILogger<WakeWordModelInstaller> logger) : IWakeWordModelInstaller
{
    private readonly string _root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "OpenClaw", "KinectSatellite", "models");

    public async Task<InstalledWakeWordModel> EnsureInstalledAsync(string id, CancellationToken cancellationToken = default)
    {
        var definition = catalog.Get(id);
        var directory = Path.Combine(_root, definition.Id);
        var metadataPath = Path.Combine(directory, "metadata.json");
        if (File.Exists(metadataPath))
        {
            var installed = JsonSerializer.Deserialize<InstalledWakeWordModel>(await File.ReadAllTextAsync(metadataPath, cancellationToken));
            if (installed is not null && File.Exists(installed.ModelPath) && await HashAsync(installed.ModelPath, cancellationToken) == installed.Sha256)
                return installed;
        }

        Directory.CreateDirectory(directory);
        var client = clients.CreateClient(nameof(WakeWordModelInstaller));
        JsonElement manifest;
        try
        {
            await using var stream = await client.GetStreamAsync(definition.ManifestUri, cancellationToken);
            manifest = (await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken)).RootElement.Clone();
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or TaskCanceledException)
        {
            throw new InvalidOperationException($"Could not download the official {definition.DisplayName} model manifest from {definition.ManifestUri}.", ex);
        }

        if (!manifest.TryGetProperty("model", out var modelProperty) || string.IsNullOrWhiteSpace(modelProperty.GetString()))
            throw new InvalidDataException($"The official {definition.DisplayName} manifest does not contain a model URL.");
        var modelReference = modelProperty.GetString()!;
        var modelUri = Uri.TryCreate(modelReference, UriKind.Absolute, out var absoluteModelUri)
            ? absoluteModelUri
            : new Uri(definition.ManifestUri, modelReference);

        // GitHub's raw endpoint does not publish a SHA-256 sidecar. Fetching the immutable model twice and
        // comparing SHA-256 digests detects incomplete/inconsistent transfers without inventing a checksum.
        var first = await client.GetByteArrayAsync(modelUri, cancellationToken);
        var second = await client.GetByteArrayAsync(modelUri, cancellationToken);
        var firstHash = Convert.ToHexString(SHA256.HashData(first)).ToLowerInvariant();
        var secondHash = Convert.ToHexString(SHA256.HashData(second)).ToLowerInvariant();
        var declaredHash = ReadDeclaredSha256(manifest);
        if (declaredHash is not null && !string.Equals(firstHash, declaredHash, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException($"The official manifest SHA-256 does not match the downloaded {definition.DisplayName} model; no model was installed.");
        if (!CryptographicOperations.FixedTimeEquals(Convert.FromHexString(firstHash), Convert.FromHexString(secondHash)))
            throw new InvalidDataException("The official model download failed SHA-256 verification; no model was installed.");

        var modelPath = Path.Combine(directory, Path.GetFileName(modelUri.LocalPath));
        await File.WriteAllBytesAsync(modelPath, first, cancellationToken);
        var licensePath = Path.Combine(directory, "LICENSE");
        await File.WriteAllBytesAsync(licensePath, await client.GetByteArrayAsync(definition.LicenseUri, cancellationToken), cancellationToken);
        await File.WriteAllTextAsync(Path.Combine(directory, "upstream-manifest.json"), JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true }), cancellationToken);
        await File.WriteAllTextAsync(Path.Combine(directory, "ATTRIBUTION.txt"), $"{definition.Attribution}{Environment.NewLine}Manifest: {definition.ManifestUri}{Environment.NewLine}Model: {modelUri}{Environment.NewLine}SHA-256: {firstHash}{Environment.NewLine}", cancellationToken);
        var result = new InstalledWakeWordModel(modelPath, firstHash, modelUri.ToString(), licensePath);
        await File.WriteAllTextAsync(metadataPath, JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }), cancellationToken);
        logger.LogInformation("Installed {WakeWord} model with SHA-256 {Sha256}", definition.DisplayName, firstHash);
        return result;
    }

    private static async Task<string> HashAsync(string path, CancellationToken token)
    {
        await using var stream = File.OpenRead(path);
        return Convert.ToHexString(await SHA256.HashDataAsync(stream, token)).ToLowerInvariant();
    }

    private static string? ReadDeclaredSha256(JsonElement manifest)
    {
        foreach (var name in new[] { "model_sha256", "sha256" })
            if (manifest.TryGetProperty(name, out var value) && value.GetString() is { Length: 64 } hash && hash.All(Uri.IsHexDigit))
                return hash.ToLowerInvariant();
        return null;
    }
}
