using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
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
    public async Task ResolveByDisplayNameAsync_returns_refname_when_field_exists()
    {
        var handler = new StubHttpHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(SchemaJson, Encoding.UTF8, "application/json")
        });
        using var http = new HttpClient(handler);
        var resolver = new AzDevOpsFieldRefNameResolver(http, NewTempCachePath());

        var refname = await resolver.ResolveByDisplayNameAsync(
            orgUrl: "https://dev.azure.com/org",
            project: "Soluciones Transversales",
            workItemType: "Task",
            displayName: "Evidencias de finalización");

        Assert.Equal("Custom.b505c83e-3745-4d8b-b76b-b3086a0c4c71", refname);
    }

    [Fact]
    public async Task ResolveByDisplayNameAsync_returns_null_when_field_absent()
    {
        var handler = new StubHttpHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""{"value": [{"name": "Title", "referenceName": "System.Title"}]}""",
                Encoding.UTF8, "application/json")
        });
        using var http = new HttpClient(handler);
        var resolver = new AzDevOpsFieldRefNameResolver(http, NewTempCachePath());

        var refname = await resolver.ResolveByDisplayNameAsync(
            "https://dev.azure.com/org", "P", "Task", "No existe");

        Assert.Null(refname);
    }

    [Fact]
    public async Task ResolveByDisplayNameAsync_caches_result_and_makes_no_second_request()
    {
        var handler = new StubHttpHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(SchemaJson, Encoding.UTF8, "application/json")
        });
        using var http = new HttpClient(handler);
        var resolver = new AzDevOpsFieldRefNameResolver(http, NewTempCachePath());

        await resolver.ResolveByDisplayNameAsync("https://dev.azure.com/org", "P", "Task", "Evidencias de finalización");
        await resolver.ResolveByDisplayNameAsync("https://dev.azure.com/org", "P", "Task", "Evidencias de finalización");

        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task ResolveByDisplayNameAsync_uses_different_cache_keys_per_project()
    {
        var handler = new StubHttpHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(SchemaJson, Encoding.UTF8, "application/json")
        });
        using var http = new HttpClient(handler);
        var resolver = new AzDevOpsFieldRefNameResolver(http, NewTempCachePath());

        await resolver.ResolveByDisplayNameAsync("https://dev.azure.com/org", "P1", "Task", "Evidencias de finalización");
        await resolver.ResolveByDisplayNameAsync("https://dev.azure.com/org", "P2", "Task", "Evidencias de finalización");

        Assert.Equal(2, handler.Requests.Count);
    }

    [Fact]
    public async Task ResolveByDisplayNameAsync_persists_cache_file_on_first_call()
    {
        var tempCache = NewTempCachePath();
        try
        {
            var handler = new StubHttpHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(SchemaJson, Encoding.UTF8, "application/json")
            });
            using var http = new HttpClient(handler);
            var resolver = new AzDevOpsFieldRefNameResolver(http, tempCache);

            var refname = await resolver.ResolveByDisplayNameAsync(
                "https://dev.azure.com/org", "P", "Task", "Effort (HH)");

            Assert.Equal("Custom.EsfuerzoEstimadoHH", refname);
            Assert.True(File.Exists(tempCache),
                $"expected cache file to be created at {tempCache}");
        }
        finally
        {
            if (File.Exists(tempCache)) File.Delete(tempCache);
        }
    }

    [Fact]
    public async Task ResolveByDisplayNameAsync_reuses_disk_cache_in_a_fresh_instance()
    {
        var tempCache = NewTempCachePath();
        try
        {
            // First instance writes the cache.
            var firstHandler = new StubHttpHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(SchemaJson, Encoding.UTF8, "application/json")
            });
            using (var http = new HttpClient(firstHandler))
            {
                var writer = new AzDevOpsFieldRefNameResolver(http, tempCache);
                await writer.ResolveByDisplayNameAsync("https://dev.azure.com/org", "P", "Task", "Effort (HH)");
            }

            // Second instance must serve from disk: handler that would fail loudly if called.
            var secondHandler = new StubHttpHandler(_ =>
                throw new InvalidOperationException("HTTP should not be called when cache is fresh"));
            using var http2 = new HttpClient(secondHandler);
            var reader = new AzDevOpsFieldRefNameResolver(http2, tempCache);

            var refname = await reader.ResolveByDisplayNameAsync(
                "https://dev.azure.com/org", "P", "Task", "Effort (HH)");

            Assert.Equal("Custom.EsfuerzoEstimadoHH", refname);
        }
        finally
        {
            if (File.Exists(tempCache)) File.Delete(tempCache);
        }
    }

    [Fact]
    public async Task ResolveByDisplayNameAsync_refetches_when_disk_cache_is_stale()
    {
        var tempCache = NewTempCachePath();
        try
        {
            // Pre-populate a stale cache entry (older than 24h) using a stale-refname
            // so we can detect whether the resolver hit the API or just read the file.
            var cacheKey = "https://dev.azure.com/org|P|Task";
            var stale = new FieldCache
            {
                Entries = new Dictionary<string, FieldCacheEntry>
                {
                    [cacheKey] = new FieldCacheEntry
                    {
                        LastCheck = DateTime.UtcNow.AddHours(-25),
                        Fields = new Dictionary<string, string>
                        {
                            ["Effort (HH)"] = "Custom.StaleRefName"
                        }
                    }
                }
            };
            await File.WriteAllTextAsync(tempCache, JsonSerializer.Serialize(stale));

            // The handler returns the *fresh* schema; if the resolver re-fetches, the
            // returned refname is the fresh one, not the stale one.
            var handler = new StubHttpHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(SchemaJson, Encoding.UTF8, "application/json")
            });
            using var http = new HttpClient(handler);
            var resolver = new AzDevOpsFieldRefNameResolver(http, tempCache);

            var refname = await resolver.ResolveByDisplayNameAsync(
                "https://dev.azure.com/org", "P", "Task", "Effort (HH)");

            Assert.Equal("Custom.EsfuerzoEstimadoHH", refname);
            Assert.Single(handler.Requests);
        }
        finally
        {
            if (File.Exists(tempCache)) File.Delete(tempCache);
        }
    }

    [Fact]
    public async Task RefreshAsync_clears_disk_cache_and_rewarms_with_a_fresh_request()
    {
        var tempCache = NewTempCachePath();
        try
        {
            // Seed an out-of-date cache (LastCheck is now, but the field map is wrong).
            var cacheKey = "https://dev.azure.com/org|P|Task";
            var seeded = new FieldCache
            {
                Entries = new Dictionary<string, FieldCacheEntry>
                {
                    [cacheKey] = new FieldCacheEntry
                    {
                        LastCheck = DateTime.UtcNow,
                        Fields = new Dictionary<string, string>
                        {
                            ["Effort (HH)"] = "Custom.StaleRefName"
                        }
                    }
                }
            };
            await File.WriteAllTextAsync(tempCache, JsonSerializer.Serialize(seeded));

            var handler = new StubHttpHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(SchemaJson, Encoding.UTF8, "application/json")
            });
            using var http = new HttpClient(handler);
            var resolver = new AzDevOpsFieldRefNameResolver(http, tempCache);

            await resolver.RefreshAsync("https://dev.azure.com/org", "P", "Task");

            // A second resolution should now be served from the freshly warmed cache,
            // i.e. no new HTTP request.
            var refname = await resolver.ResolveByDisplayNameAsync(
                "https://dev.azure.com/org", "P", "Task", "Effort (HH)");

            Assert.Equal("Custom.EsfuerzoEstimadoHH", refname);
            Assert.Single(handler.Requests);
        }
        finally
        {
            if (File.Exists(tempCache)) File.Delete(tempCache);
        }
    }

    [Fact]
    public async Task ResolveByDisplayNameAsync_isolates_disk_cache_per_org_project_and_type()
    {
        var tempCache = NewTempCachePath();
        try
        {
            // Seed two distinct entries: one project has the field, the other does not.
            var seeded = new FieldCache
            {
                Entries = new Dictionary<string, FieldCacheEntry>
                {
                    ["https://dev.azure.com/orgA|P1|Task"] = new FieldCacheEntry
                    {
                        LastCheck = DateTime.UtcNow,
                        Fields = new Dictionary<string, string> { ["Effort (HH)"] = "Custom.P1RefName" }
                    },
                    ["https://dev.azure.com/orgA|P1|Bug"] = new FieldCacheEntry
                    {
                        LastCheck = DateTime.UtcNow,
                        Fields = new Dictionary<string, string>()
                    },
                    ["https://dev.azure.com/orgB|P1|Task"] = new FieldCacheEntry
                    {
                        LastCheck = DateTime.UtcNow,
                        Fields = new Dictionary<string, string> { ["Effort (HH)"] = "Custom.P1OrgBRefName" }
                    }
                }
            };
            await File.WriteAllTextAsync(tempCache, JsonSerializer.Serialize(seeded));

            // Handler that fails loudly if the API is hit (cache should be used).
            var handler = new StubHttpHandler(_ =>
                throw new InvalidOperationException("HTTP should not be called when cache has all needed entries"));
            using var http = new HttpClient(handler);
            var resolver = new AzDevOpsFieldRefNameResolver(http, tempCache);

            Assert.Equal("Custom.P1RefName",
                await resolver.ResolveByDisplayNameAsync("https://dev.azure.com/orgA", "P1", "Task", "Effort (HH)"));
            Assert.Equal("Custom.P1OrgBRefName",
                await resolver.ResolveByDisplayNameAsync("https://dev.azure.com/orgB", "P1", "Task", "Effort (HH)"));
            Assert.Null(
                await resolver.ResolveByDisplayNameAsync("https://dev.azure.com/orgA", "P1", "Bug", "Effort (HH)"));
        }
        finally
        {
            if (File.Exists(tempCache)) File.Delete(tempCache);
        }
    }

    [Fact]
    public async Task ResolveByDisplayNameAsync_refetches_after_api_error_and_does_not_cache_empty()
    {
        var tempCache = NewTempCachePath();
        try
        {
            var responses = new Queue<HttpResponseMessage>(new[]
            {
                // First call: API error (e.g. TF51535 for an unknown workitemtype).
                new HttpResponseMessage(HttpStatusCode.BadRequest)
                {
                    Content = new StringContent(
                        """{"message":"TF51535: ...","typeName":"Task","fieldName":"Effort"}""",
                        Encoding.UTF8, "application/json")
                },
                // Second call: API succeeds with the schema.
                new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(SchemaJson, Encoding.UTF8, "application/json")
                }
            });
            var handler = new StubHttpHandler(_ => responses.Dequeue());
            using var http = new HttpClient(handler);
            var resolver = new AzDevOpsFieldRefNameResolver(http, tempCache);

            var first = await resolver.ResolveByDisplayNameAsync(
                "https://dev.azure.com/org", "P", "Task", "Effort (HH)");
            var second = await resolver.ResolveByDisplayNameAsync(
                "https://dev.azure.com/org", "P", "Task", "Effort (HH)");

            Assert.Null(first);                            // error → null
            Assert.Equal("Custom.EsfuerzoEstimadoHH", second); // re-fetch succeeded
            Assert.Equal(2, handler.Requests.Count);
        }
        finally
        {
            if (File.Exists(tempCache)) File.Delete(tempCache);
        }
    }

    [Fact]
    public async Task ResolveByDisplayNameAsync_keeps_same_project_in_different_orgs_separate()
    {
        var tempCache = NewTempCachePath();
        try
        {
            var seeded = new FieldCache
            {
                Entries = new Dictionary<string, FieldCacheEntry>
                {
                    ["https://dev.azure.com/orgA|P|Task"] = new FieldCacheEntry
                    {
                        LastCheck = DateTime.UtcNow,
                        Fields = new Dictionary<string, string> { ["Effort (HH)"] = "Custom.OrgA.Effort" }
                    },
                    ["https://dev.azure.com/orgB|P|Task"] = new FieldCacheEntry
                    {
                        LastCheck = DateTime.UtcNow,
                        Fields = new Dictionary<string, string> { ["Effort (HH)"] = "Custom.OrgB.Effort" }
                    }
                }
            };
            await File.WriteAllTextAsync(tempCache, JsonSerializer.Serialize(seeded));

            var handler = new StubHttpHandler(_ =>
                throw new InvalidOperationException("HTTP should not be called when entries are in cache"));
            using var http = new HttpClient(handler);
            var resolver = new AzDevOpsFieldRefNameResolver(http, tempCache);

            Assert.Equal("Custom.OrgA.Effort",
                await resolver.ResolveByDisplayNameAsync("https://dev.azure.com/orgA", "P", "Task", "Effort (HH)"));
            Assert.Equal("Custom.OrgB.Effort",
                await resolver.ResolveByDisplayNameAsync("https://dev.azure.com/orgB", "P", "Task", "Effort (HH)"));
        }
        finally
        {
            if (File.Exists(tempCache)) File.Delete(tempCache);
        }
    }

    private static string NewTempCachePath()
        => Path.Combine(Path.GetTempPath(), $"yp-field-cache-{Guid.NewGuid():N}.json");
}
