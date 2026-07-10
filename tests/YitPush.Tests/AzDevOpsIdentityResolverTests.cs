using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using YitPush;

namespace YitPush.Tests;

public class AzDevOpsIdentityResolverTests
{
    private const string IdentitiesSingleJson = """
    {
      "value": [
        {
          "id": "00000000-0000-0000-0000-000000000abc",
          "displayName": "Elvis Brevi",
          "uniqueName": "elvis.brevi@sag.gob.cl",
          "descriptor": "vssgp.Uy0xLTktMTU1MTM3NDIyNS0xMjA0NDAwOTY5"
        }
      ]
    }
    """;

    private const string IdentitiesMultipleJson = """
    {
      "value": [
        { "id": "00000000-0000-0000-0000-000000000aa1", "displayName": "Juan Pérez", "uniqueName": "juan.perez@org.cl", "descriptor": "vssgp.A" },
        { "id": "00000000-0000-0000-0000-000000000aa2", "displayName": "Juan Pérez", "uniqueName": "juan.perez2@org.cl", "descriptor": "vssgp.B" }
      ]
    }
    """;

    private const string IdentitiesEmptyJson = """{"value": []}""";

    [Fact]
    public async Task ResolveAsync_returns_single_match_when_identities_api_returns_one()
    {
        var handler = new StubHttpHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(IdentitiesSingleJson, Encoding.UTF8, "application/json")
        });
        using var http = new HttpClient(handler);
        var resolver = new AzDevOpsIdentityResolver(http);

        var result = await resolver.ResolveAsync("https://dev.azure.com/org", "Elvis Brevi");

        var single = Assert.IsType<IdentityResolutionResult.SingleMatch>(result);
        Assert.Equal("Elvis Brevi", single.Match.DisplayName);
        Assert.Equal("elvis.brevi@sag.gob.cl", single.Match.UniqueName);
        Assert.Equal("00000000-0000-0000-0000-000000000abc", single.Match.Id);
    }

    [Fact]
    public async Task ResolveAsync_returns_multiple_when_identities_api_returns_several()
    {
        var handler = new StubHttpHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(IdentitiesMultipleJson, Encoding.UTF8, "application/json")
        });
        using var http = new HttpClient(handler);
        var resolver = new AzDevOpsIdentityResolver(http);

        var result = await resolver.ResolveAsync("https://dev.azure.com/org", "Juan Pérez");

        var multiple = Assert.IsType<IdentityResolutionResult.MultipleMatches>(result);
        Assert.Equal(2, multiple.Matches.Count);
    }

    [Fact]
    public async Task ResolveAsync_returns_not_found_when_identities_api_returns_empty()
    {
        var handler = new StubHttpHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(IdentitiesEmptyJson, Encoding.UTF8, "application/json")
        });
        using var http = new HttpClient(handler);
        var resolver = new AzDevOpsIdentityResolver(http);

        var result = await resolver.ResolveAsync("https://dev.azure.com/org", "Nobody");

        Assert.IsType<IdentityResolutionResult.NotFound>(result);
    }

    [Fact]
    public async Task ResolveAsync_returns_clear_for_empty_input()
    {
        var handler = new StubHttpHandler(_ => throw new InvalidOperationException("HTTP should not be called for empty input"));
        using var http = new HttpClient(handler);
        var resolver = new AzDevOpsIdentityResolver(http);

        var result = await resolver.ResolveAsync("https://dev.azure.com/org", "");

        Assert.IsType<IdentityResolutionResult.Clear>(result);
    }

    [Fact]
    public async Task ResolveAsync_returns_clear_for_whitespace_input()
    {
        var handler = new StubHttpHandler(_ => throw new InvalidOperationException("HTTP should not be called for whitespace input"));
        using var http = new HttpClient(handler);
        var resolver = new AzDevOpsIdentityResolver(http);

        var result = await resolver.ResolveAsync("https://dev.azure.com/org", "   ");

        Assert.IsType<IdentityResolutionResult.Clear>(result);
    }

    [Fact]
    public async Task ResolveAsync_caches_result_to_avoid_repeat_api_calls()
    {
        var handler = new StubHttpHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(IdentitiesSingleJson, Encoding.UTF8, "application/json")
        });
        using var http = new HttpClient(handler);
        var resolver = new AzDevOpsIdentityResolver(http);

        var first = await resolver.ResolveAsync("https://dev.azure.com/org", "Elvis Brevi");
        var second = await resolver.ResolveAsync("https://dev.azure.com/org", "Elvis Brevi");

        Assert.IsType<IdentityResolutionResult.SingleMatch>(first);
        Assert.IsType<IdentityResolutionResult.SingleMatch>(second);
        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task ResolveAsync_treats_input_as_case_insensitive_in_cache_key()
    {
        var handler = new StubHttpHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(IdentitiesSingleJson, Encoding.UTF8, "application/json")
        });
        using var http = new HttpClient(handler);
        var resolver = new AzDevOpsIdentityResolver(http);

        await resolver.ResolveAsync("https://dev.azure.com/org", "elvis brevi");
        await resolver.ResolveAsync("https://dev.azure.com/org", "ELVIS BREVI");

        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task ResolveAsync_uses_separate_cache_keys_per_org()
    {
        var handler = new StubHttpHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(IdentitiesSingleJson, Encoding.UTF8, "application/json")
        });
        using var http = new HttpClient(handler);
        var resolver = new AzDevOpsIdentityResolver(http);

        await resolver.ResolveAsync("https://dev.azure.com/orgA", "Elvis Brevi");
        await resolver.ResolveAsync("https://dev.azure.com/orgB", "Elvis Brevi");

        Assert.Equal(2, handler.Requests.Count);
    }
}
