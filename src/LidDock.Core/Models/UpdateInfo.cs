using System;
using System.Text.Json.Serialization;

namespace LidDock.Core.Models;

public class gitHubReleaseInfo
{
    [JsonPropertyName("tag_name")]
    public string tagName { get; set; } = string.Empty;

    [JsonPropertyName("html_url")]
    public string htmlUrl { get; set; } = string.Empty;

    [JsonPropertyName("body")]
    public string body { get; set; } = string.Empty;

    [JsonPropertyName("prerelease")]
    public bool prerelease { get; set; }
}

public class appUpdateResult
{
    public bool isUpdateAvailable { get; }
    public Version? latestVersion { get; }
    public string releaseUrl { get; }
    public string releaseNotes { get; }
    public string? errorMessage { get; }

    public appUpdateResult(bool isUpdateAvailable, Version? latestVersion, string releaseUrl, string releaseNotes, string? errorMessage = null)
    {
        this.isUpdateAvailable = isUpdateAvailable;
        this.latestVersion = latestVersion;
        this.releaseUrl = releaseUrl;
        this.releaseNotes = releaseNotes;
        this.errorMessage = errorMessage;
    }
}
