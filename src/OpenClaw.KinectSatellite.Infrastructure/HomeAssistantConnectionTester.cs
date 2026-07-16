using System.Net.Http.Headers;
using OpenClaw.KinectSatellite.Core;

namespace OpenClaw.KinectSatellite.Infrastructure;

public sealed class HomeAssistantConnectionTester(IHttpClientFactory clients) : IHomeAssistantConnectionTester
{
    public async Task<(bool Success, string Message)> TestAsync(string url, string accessToken, CancellationToken cancellationToken = default)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var baseUri) || baseUri.Scheme is not ("http" or "https"))
            return (false, "Enter a valid HTTP or HTTPS Home Assistant URL.");
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(baseUri, "/api/"));
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            using var response = await clients.CreateClient(nameof(HomeAssistantConnectionTester)).SendAsync(request, cancellationToken);
            return response.IsSuccessStatusCode
                ? (true, "Connected to Home Assistant successfully.")
                : (false, $"Home Assistant returned {(int)response.StatusCode} {response.ReasonPhrase}.");
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            return (false, $"Connection failed: {ex.Message}");
        }
    }
}
