using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using LidDock.Core.Contracts;
using LidDock.Core.Models;

namespace LidDock.Core.Services;

public class updateChecker : iUpdateChecker, IDisposable
{
    private readonly HttpClient httpClient;
    private readonly bool shouldDisposeClient;
    private const string defaultReleasesApiUrl = "https://api.github.com/repos/MeowIce/LidDock/releases/latest";
    private readonly string releasesApiUrl;

    public updateChecker(HttpClient? customClient = null, string? customUrl = null)
    {
        shouldDisposeClient = customClient == null;
        httpClient = customClient ?? createDefaultHttpClient();
        releasesApiUrl = customUrl ?? defaultReleasesApiUrl;
    }

    private static HttpClient createDefaultHttpClient()
    {
        var handler = new SocketsHttpHandler
        {
            PooledConnectionLifetime = TimeSpan.FromSeconds(5),
            PooledConnectionIdleTimeout = TimeSpan.FromSeconds(2),
            EnableMultipleHttp2Connections = false
        };
        return new HttpClient(handler, disposeHandler: true);
    }

    public void Dispose()
    {
        if (shouldDisposeClient)
        {
            httpClient.Dispose();
        }
    }

    public async Task<appUpdateResult> checkForUpdatesAsync(Version currentVersion, CancellationToken cancellationToken = default)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, releasesApiUrl);
            request.Headers.Add("User-Agent", "LidDock-App");
            request.Headers.Add("Accept", "application/vnd.github.v3+json");

            using var response = await httpClient.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return new appUpdateResult(false, null, string.Empty, string.Empty, $"Server responded with {(int)response.StatusCode}");
            }

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            var release = JsonSerializer.Deserialize(json, appSettingsJsonContext.Default.gitHubReleaseInfo);
            if (release == null || string.IsNullOrEmpty(release.tagName))
            {
                return new appUpdateResult(false, null, string.Empty, string.Empty, "No release data found");
            }

            var cleanTag = release.tagName.TrimStart('v', 'V').Split('-')[0];
            if (Version.TryParse(cleanTag, out var remoteVersion))
            {
                var isAvailable = remoteVersion > currentVersion;
                return new appUpdateResult(isAvailable, remoteVersion, release.htmlUrl, release.body);
            }

            return new appUpdateResult(false, null, release.htmlUrl, release.body, "Unable to parse remote version tag");
        }
        catch (Exception ex)
        {
            return new appUpdateResult(false, null, string.Empty, string.Empty, ex.Message);
        }
    }
}
