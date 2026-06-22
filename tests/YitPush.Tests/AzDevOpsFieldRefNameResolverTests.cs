using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using YitPush;

namespace YitPush.Tests;

internal class StubHttpHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, HttpResponseMessage> _handler;

    public StubHttpHandler(Func<HttpRequestMessage, HttpResponseMessage> handler)
    {
        _handler = handler;
    }

    public List<HttpRequestMessage> Requests { get; } = new();

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Requests.Add(request);
        return Task.FromResult(_handler(request));
    }
}

public class AzDevOpsFieldRefNameResolverTests
{
    private const string SchemaJson = """
    {
      "value": [
        { "name": "Title", "referenceName": "System.Title" },
        { "name": "Evidencias de finalización", "referenceName": "Custom.b505c83e-3745-4d8b-b76b-b3086a0c4c71" },
        { "name": "Effort (HH)", "referenceName": "Custom.EsfuerzoEstimadoHH" }
      ]
    }
    """;

    [Fact]
    public async Task ResolveAsync_returns_refname_when_field_exists()
    {
        var handler = new StubHttpHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(SchemaJson, Encoding.UTF8, "application/json")
        });
        using var http = new HttpClient(handler);
        var resolver = new AzDevOpsFieldRefNameResolver(http);

        var refname = await resolver.ResolveAsync(
            orgUrl: "https://dev.azure.com/org",
            project: "Soluciones Transversales",
            workItemType: "Task",
            displayName: "Evidencias de finalización");

        Assert.Equal("Custom.b505c83e-3745-4d8b-b76b-b3086a0c4c71", refname);
    }

    [Fact]
    public async Task ResolveAsync_returns_null_when_field_absent()
    {
        var handler = new StubHttpHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""{"value": [{"name": "Title", "referenceName": "System.Title"}]}""",
                Encoding.UTF8, "application/json")
        });
        using var http = new HttpClient(handler);
        var resolver = new AzDevOpsFieldRefNameResolver(http);

        var refname = await resolver.ResolveAsync(
            "https://dev.azure.com/org", "P", "Task", "No existe");

        Assert.Null(refname);
    }

    [Fact]
    public async Task ResolveAsync_caches_result_and_makes_no_second_request()
    {
        var handler = new StubHttpHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(SchemaJson, Encoding.UTF8, "application/json")
        });
        using var http = new HttpClient(handler);
        var resolver = new AzDevOpsFieldRefNameResolver(http);

        await resolver.ResolveAsync("https://dev.azure.com/org", "P", "Task", "Evidencias de finalización");
        await resolver.ResolveAsync("https://dev.azure.com/org", "P", "Task", "Evidencias de finalización");

        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task ResolveAsync_uses_different_cache_keys_per_project()
    {
        var handler = new StubHttpHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(SchemaJson, Encoding.UTF8, "application/json")
        });
        using var http = new HttpClient(handler);
        var resolver = new AzDevOpsFieldRefNameResolver(http);

        await resolver.ResolveAsync("https://dev.azure.com/org", "P1", "Task", "Evidencias de finalización");
        await resolver.ResolveAsync("https://dev.azure.com/org", "P2", "Task", "Evidencias de finalización");

        Assert.Equal(2, handler.Requests.Count);
    }
}
