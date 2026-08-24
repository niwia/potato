using System.Net;
using FluentAssertions;
using Potato.Domain.ValueObjects;
using Potato.ManifestApi.Client;
using Potato.ManifestApi.Models;
using Xunit;

namespace Potato.ManifestApi.Tests.Client;

public class HubcapApiClientSearchTests
{
    private sealed class MockHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _handler;

        public MockHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> handler)
        {
            _handler = handler;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(_handler(request));
        }
    }

    [Fact]
    public async Task SearchGamesAsync_ShouldReturnParsedResults_WhenApiReturnsJson()
    {
        string json = """
        {
            "results": [
                {
                    "app_id": "603960",
                    "name": "Star of Providence",
                    "manifest_size": 250000000,
                    "manifest_available": true,
                    "image": "https://steamstatic.com/header.jpg"
                }
            ],
            "total_matches": 1
        }
        """;

        var handler = new MockHttpMessageHandler(req => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
        });

        var client = new HubcapApiClient(new HttpClient(handler), options: new HubcapApiOptions { ApiKey = "test_key" });
        var results = await client.SearchGamesAsync("Providence");

        results.Should().HaveCount(1);
        results[0].AppId.Should().Be(new AppId(603960));
        results[0].Name.Should().Be("Star of Providence");
        results[0].ManifestAvailable.Should().BeTrue();
    }

    [Fact]
    public async Task GetAllStatsAsync_ShouldParseUserStatsAndGenerateUsage()
    {
        var handler = new MockHttpMessageHandler(req =>
        {
            if (req.RequestUri!.AbsolutePath.Contains("/user/stats"))
            {
                string userJson = """{"daily_manifest_downloads": 5, "daily_manifest_limit": 55, "expires_at": "2027-01-01T00:00:00Z"}""";
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(userJson, System.Text.Encoding.UTF8, "application/json")
                };
            }
            if (req.RequestUri!.AbsolutePath.Contains("/generate/usage"))
            {
                string genJson = """{"app_bundle_usage": 2, "app_bundle_limit": 100, "single_depot_usage": 10, "single_depot_limit": 1500}""";
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(genJson, System.Text.Encoding.UTF8, "application/json")
                };
            }

            return new HttpResponseMessage(HttpStatusCode.OK);
        });

        var client = new HubcapApiClient(new HttpClient(handler), options: new HubcapApiOptions { ApiKey = "test_key" });
        var stats = await client.GetAllStatsAsync();

        stats.UserStats.DailyManifestDownloads.Should().Be(5);
        stats.UserStats.DailyManifestLimit.Should().Be(55);
        stats.GenerateUsage.AppBundleUsage.Should().Be(2);
        stats.GenerateUsage.SingleDepotUsage.Should().Be(10);
        stats.FormattedQuotaString.Should().Contain("api: 5/55");
        stats.FormattedQuotaString.Should().Contain("bundle: 2/100");
        stats.FormattedQuotaString.Should().Contain("single: 10/1500");
    }
}
