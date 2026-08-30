using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using LidDock.Core.Services;
using Xunit;

namespace LidDock.Tests;

public class fakeHttpMessageHandler : HttpMessageHandler
{
    private readonly string responseContent;
    private readonly HttpStatusCode statusCode;

    public fakeHttpMessageHandler(string responseContent, HttpStatusCode statusCode = HttpStatusCode.OK)
    {
        this.responseContent = responseContent;
        this.statusCode = statusCode;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var response = new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(responseContent)
        };
        return Task.FromResult(response);
    }
}

public class updateCheckerTests
{
    [Fact]
    public async Task shouldDetectUpdateAvailableWhenRemoteVersionIsHigher()
    {
        var json = "{\"tag_name\":\"v1.2.0\",\"html_url\":\"https://github.com/MeowIce/LidDock/releases/tag/v1.2.0\",\"body\":\"Release notes\",\"prerelease\":false}";
        var handler = new fakeHttpMessageHandler(json);
        var client = new HttpClient(handler);
        var checker = new updateChecker(client);

        var currentVersion = new Version("1.0.0");
        var result = await checker.checkForUpdatesAsync(currentVersion);

        Assert.True(result.isUpdateAvailable);
        Assert.Equal(new Version("1.2.0"), result.latestVersion);
        Assert.Equal("https://github.com/MeowIce/LidDock/releases/tag/v1.2.0", result.releaseUrl);
    }

    [Fact]
    public async Task shouldDetectNoUpdateWhenRemoteVersionIsEqualOrLower()
    {
        var json = "{\"tag_name\":\"v1.0.0\",\"html_url\":\"https://github.com/MeowIce/LidDock/releases/tag/v1.0.0\",\"body\":\"Release notes\",\"prerelease\":false}";
        var handler = new fakeHttpMessageHandler(json);
        var client = new HttpClient(handler);
        var checker = new updateChecker(client);

        var currentVersion = new Version("1.0.0");
        var result = await checker.checkForUpdatesAsync(currentVersion);

        Assert.False(result.isUpdateAvailable);
        Assert.Equal(new Version("1.0.0"), result.latestVersion);
    }

    [Fact]
    public async Task shouldHandleHttpErrorGracefully()
    {
        var handler = new fakeHttpMessageHandler("Not Found", HttpStatusCode.NotFound);
        var client = new HttpClient(handler);
        var checker = new updateChecker(client);

        var currentVersion = new Version("1.0.0");
        var result = await checker.checkForUpdatesAsync(currentVersion);

        Assert.False(result.isUpdateAvailable);
        Assert.NotNull(result.errorMessage);
    }

    [Fact]
    public async Task shouldParsePrereleaseTagCorrectly()
    {
        var json = "{\"tag_name\":\"v1.0.1-DEV-1\",\"html_url\":\"https://github.com/MeowIce/LidDock/releases/tag/v1.0.1-DEV-1\",\"body\":\"Release notes\",\"prerelease\":true}";
        var handler = new fakeHttpMessageHandler(json);
        var client = new HttpClient(handler);
        var checker = new updateChecker(client);

        var currentVersion = new Version("1.0.0");
        var result = await checker.checkForUpdatesAsync(currentVersion);

        Assert.True(result.isUpdateAvailable);
        Assert.Equal(new Version("1.0.1"), result.latestVersion);
    }
}
