using System;
using System.Text.Json.Serialization;

namespace LidDock.Core.Models;

public class gitHubReleaseInfo
{
    [JsonPropertyName("tag_name")]
    public string tagName { get; set; } = string.Empty;

    [JsonPropertyName("html_url")]
    public string htmlUrl { get; set; } = string.Empty;
}

public class appUpdateResult
{
    public bool isUpdateAvailable { get; }
    public Version? latestVersion { get; }
    public string releaseUrl { get; }
    public string? errorMessage { get; }

    public appUpdateResult(bool isUpdateAvailable, Version? latestVersion, string releaseUrl, string? errorMessage = null)
    {
        this.isUpdateAvailable = isUpdateAvailable;
        this.latestVersion = latestVersion;
        this.releaseUrl = releaseUrl;
        this.errorMessage = errorMessage;
    }
}
