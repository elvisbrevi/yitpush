using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using YitPush;

namespace YitPush.Tests;

public class AzDevOpsStateCacheTests
{
    private const string StatesJson = """
    {
      "value": [
        { "name": "To Do", "color": "b2b2b2", "category": "Proposed" },
        { "name": "Doing", "color": "b2b2b2", "category": "InProgress" },
        { "name": "En revisión", "color": "b2b2b2", "category": "InProgress" },
        { "name": "Done", "color": "b2b2b2", "category": "Completed" }
      ]
    }
    """;

    private static string NewTempCachePath()
        => Path.Combine(Path.GetTempPath(), $"yp-state-cache-{Guid.NewGuid():N}.json");

    [Fact]
    public async Task GetValidStatesAsync_returns_states_from_api()
    {
        var handler = new StubHttpHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(StatesJson, Encoding.UTF8, "application/json")
        });
        using var http = new HttpClient(handler);
        var cache = new AzDevOpsStateCache(http, NewTempCachePath());

        var states = await cache.GetValidStatesAsync("https://dev.azure.com/org", "P", "Task");

        Assert.Contains("To Do", states);
        Assert.Contains("Doing", states);
        Assert.Contains("En revisión", states);
        Assert.Contains("Done", states);
    }

    [Fact]
    public async Task GetValidStatesAsync_caches_in_memory_and_does_not_refetch()
    {
        var handler = new StubHttpHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(StatesJson, Encoding.UTF8, "application/json")
        });
        using var http = new HttpClient(handler);
        var cache = new AzDevOpsStateCache(http, NewTempCachePath());

        await cache.GetValidStatesAsync("https://dev.azure.com/org", "P", "Task");
        await cache.GetValidStatesAsync("https://dev.azure.com/org", "P", "Task");

        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task GetValidStatesAsync_returns_empty_set_on_api_failure_and_does_not_throw()
    {
        var handler = new StubHttpHandler(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError)
        {
            Content = new StringContent("oops", Encoding.UTF8, "text/plain")
        });
        using var http = new HttpClient(handler);
        var cache = new AzDevOpsStateCache(http, NewTempCachePath());

        var states = await cache.GetValidStatesAsync("https://dev.azure.com/org", "P", "Task");

        Assert.Empty(states);
    }

    [Fact]
    public async Task GetValidStatesAsync_uses_separate_cache_keys_per_project()
    {
        var handler = new StubHttpHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(StatesJson, Encoding.UTF8, "application/json")
        });
        using var http = new HttpClient(handler);
        var cache = new AzDevOpsStateCache(http, NewTempCachePath());

        await cache.GetValidStatesAsync("https://dev.azure.com/org", "P1", "Task");
        await cache.GetValidStatesAsync("https://dev.azure.com/org", "P2", "Task");

        Assert.Equal(2, handler.Requests.Count);
    }

    [Fact]
    public async Task IsLikelyValidStateAsync_accepts_localized_state_when_cached()
    {
        var handler = new StubHttpHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(StatesJson, Encoding.UTF8, "application/json")
        });
        using var http = new HttpClient(handler);
        var cache = new AzDevOpsStateCache(http, NewTempCachePath());

        var ok = await cache.IsLikelyValidStateAsync("https://dev.azure.com/org", "P", "Task", "En revisión");
        Assert.True(ok);
    }

    [Fact]
    public async Task IsLikelyValidStateAsync_rejects_unknown_state_when_cached()
    {
        var handler = new StubHttpHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(StatesJson, Encoding.UTF8, "application/json")
        });
        using var http = new HttpClient(handler);
        var cache = new AzDevOpsStateCache(http, NewTempCachePath());

        var ok = await cache.IsLikelyValidStateAsync("https://dev.azure.com/org", "P", "Task", "Bogus");
        Assert.False(ok);
    }

    [Fact]
    public async Task IsLikelyValidStateAsync_returns_true_on_api_failure_to_avoid_blocking_patch()
    {
        var handler = new StubHttpHandler(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError)
        {
            Content = new StringContent("oops", Encoding.UTF8, "text/plain")
        });
        using var http = new HttpClient(handler);
        var cache = new AzDevOpsStateCache(http, NewTempCachePath());

        // fail open: if we cannot reach the API, never block the PATCH
        var ok = await cache.IsLikelyValidStateAsync("https://dev.azure.com/org", "P", "Task", "Anything");
        Assert.True(ok);
    }
}
