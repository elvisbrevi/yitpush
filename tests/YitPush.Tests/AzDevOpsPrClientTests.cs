using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using YitPush;

namespace YitPush.Tests;

public class AzDevOpsPrClientTests
{
    [Fact]
    public async Task CreatePullRequestAsync_sends_POST_to_pullRequests_endpoint_with_expected_body()
    {
        var captured = new Dictionary<string, object?>();
        var handler = new StubHttpHandler(req =>
        {
            captured["method"] = req.Method;
            captured["url"] = req.RequestUri?.ToString();
            captured["body"] = req.Content?.ReadAsStringAsync().GetAwaiter().GetResult();
            return new HttpResponseMessage(HttpStatusCode.Created)
            {
                Content = new StringContent(
                    """{"pullRequestId":4242,"status":"active","title":"feat: x"}""",
                    Encoding.UTF8, "application/json")
            };
        });
        using var http = new HttpClient(handler);

        var result = await AzDevOpsPrClient.CreatePullRequestAsync(
            http,
            orgUrl: "https://dev.azure.com/org",
            projectId: "proj-guid",
            repoId: "repo-guid",
            request: new AzDevOpsPrClient.CreatePrRequest(
                SourceRefName: "refs/heads/feature/x",
                TargetRefName: "refs/heads/main",
                Title: "feat: x",
                Description: "body content",
                AutoComplete: false));

        Assert.Equal(HttpMethod.Post, captured["method"]);
        var url = captured["url"]?.ToString() ?? "";
        Assert.Contains("/proj-guid/_apis/git/repositories/repo-guid/pullRequests", url);
        Assert.Contains("api-version=7.1-preview.1", url);
        var body = captured["body"]?.ToString() ?? "";
        Assert.Contains("\"sourceRefName\":\"refs/heads/feature/x\"", body);
        Assert.Contains("\"targetRefName\":\"refs/heads/main\"", body);
        Assert.Contains("\"title\":\"feat: x\"", body);
        Assert.Contains("\"description\":\"body content\"", body);
    }

    [Fact]
    public async Task CreatePullRequestAsync_returns_zero_and_pullRequestId_on_2xx()
    {
        var handler = new StubHttpHandler(_ => new HttpResponseMessage(HttpStatusCode.Created)
        {
            Content = new StringContent("""{"pullRequestId":7777}""", Encoding.UTF8, "application/json")
        });
        using var http = new HttpClient(handler);

        var result = await AzDevOpsPrClient.CreatePullRequestAsync(
            http, "https://dev.azure.com/org", "proj", "repo",
            new AzDevOpsPrClient.CreatePrRequest("refs/heads/a", "refs/heads/b", "t", "d", false));

        Assert.Equal(0, result.ExitCode);
        Assert.Equal("7777", result.PullRequestId);
        Assert.Null(result.Error);
    }

    [Fact]
    public async Task CreatePullRequestAsync_returns_exit_code_1_on_404()
    {
        var handler = new StubHttpHandler(_ => new HttpResponseMessage(HttpStatusCode.NotFound)
        {
            Content = new StringContent("""{"message":"repository not found"}""", Encoding.UTF8, "application/json")
        });
        using var http = new HttpClient(handler);

        var result = await AzDevOpsPrClient.CreatePullRequestAsync(
            http, "https://dev.azure.com/org", "proj", "missing-repo",
            new AzDevOpsPrClient.CreatePrRequest("refs/heads/a", "refs/heads/b", "t", "d", false));

        Assert.Equal(1, result.ExitCode);
    }

    [Fact]
    public async Task CreatePullRequestAsync_returns_exit_code_2_on_401()
    {
        var handler = new StubHttpHandler(_ => new HttpResponseMessage(HttpStatusCode.Unauthorized)
        {
            Content = new StringContent("TF400813: anonymous access denied", Encoding.UTF8, "text/plain")
        });
        using var http = new HttpClient(handler);

        var result = await AzDevOpsPrClient.CreatePullRequestAsync(
            http, "https://dev.azure.com/org", "proj", "repo",
            new AzDevOpsPrClient.CreatePrRequest("refs/heads/a", "refs/heads/b", "t", "d", false));

        Assert.Equal(2, result.ExitCode);
        Assert.NotNull(result.Error);
        Assert.Contains("TF400813", result.Error);
    }

    [Fact]
    public async Task CreatePullRequestAsync_sets_completionOptions_when_autoComplete_is_true()
    {
        var captured = new Dictionary<string, object?>();
        var handler = new StubHttpHandler(req =>
        {
            captured["body"] = req.Content?.ReadAsStringAsync().GetAwaiter().GetResult();
            return new HttpResponseMessage(HttpStatusCode.Created)
            {
                Content = new StringContent("""{"pullRequestId":1}""", Encoding.UTF8, "application/json")
            };
        });
        using var http = new HttpClient(handler);

        await AzDevOpsPrClient.CreatePullRequestAsync(
            http, "https://dev.azure.com/org", "proj", "repo",
            new AzDevOpsPrClient.CreatePrRequest("refs/heads/a", "refs/heads/b", "t", "d", AutoComplete: true));

        var body = captured["body"]?.ToString() ?? "";
        Assert.Contains("\"completionOptions\"", body);
        Assert.Contains("\"deleteSourceBranch\":true", body);
    }

    // ── PostThreadCommentAsync ────────────────────────────────────────────────

    [Fact]
    public async Task PostThreadCommentAsync_sends_POST_to_thread_comments_endpoint_with_expected_body()
    {
        var captured = new Dictionary<string, object?>();
        var handler = new StubHttpHandler(req =>
        {
            captured["method"] = req.Method;
            captured["url"] = req.RequestUri?.ToString();
            captured["body"] = req.Content?.ReadAsStringAsync().GetAwaiter().GetResult();
            return new HttpResponseMessage(HttpStatusCode.Created)
            {
                Content = new StringContent("""{"id":99,"content":"ok"}""", Encoding.UTF8, "application/json")
            };
        });
        using var http = new HttpClient(handler);

        var result = await AzDevOpsPrClient.PostThreadCommentAsync(
            http, "https://dev.azure.com/org", "proj-guid", "repo-guid", prId: 12345, threadId: 678,
            body: "Fixed in commit abc");

        Assert.Equal(0, result.ExitCode);
        Assert.Equal(HttpMethod.Post, captured["method"]);
        var url = captured["url"]?.ToString() ?? "";
        Assert.Contains("/proj-guid/_apis/git/repositories/repo-guid/pullRequests/12345/threads/678/comments", url);
        Assert.Contains("api-version=7.1-preview.1", url);
        var bodyStr = captured["body"]?.ToString() ?? "";
        Assert.Contains("\"content\":\"Fixed in commit abc\"", bodyStr);
    }

    [Fact]
    public async Task PostThreadCommentAsync_returns_exit_code_1_on_404()
    {
        var handler = new StubHttpHandler(_ => new HttpResponseMessage(HttpStatusCode.NotFound)
        {
            Content = new StringContent("thread not found", Encoding.UTF8, "text/plain")
        });
        using var http = new HttpClient(handler);

        var result = await AzDevOpsPrClient.PostThreadCommentAsync(
            http, "https://dev.azure.com/org", "proj", "repo", 1, 9999, "body");

        Assert.Equal(1, result.ExitCode);
    }

    [Fact]
    public async Task PostThreadCommentAsync_returns_exit_code_2_on_401()
    {
        var handler = new StubHttpHandler(_ => new HttpResponseMessage(HttpStatusCode.Unauthorized));
        using var http = new HttpClient(handler);

        var result = await AzDevOpsPrClient.PostThreadCommentAsync(
            http, "https://dev.azure.com/org", "proj", "repo", 1, 2, "body");

        Assert.Equal(2, result.ExitCode);
    }

    // ── ListThreadsAsync ──────────────────────────────────────────────────────

    [Fact]
    public async Task ListThreadsAsync_sends_GET_to_threads_endpoint()
    {
        var captured = new Dictionary<string, object?>();
        var handler = new StubHttpHandler(req =>
        {
            captured["method"] = req.Method;
            captured["url"] = req.RequestUri?.ToString();
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"value":[]}""", Encoding.UTF8, "application/json")
            };
        });
        using var http = new HttpClient(handler);

        await AzDevOpsPrClient.ListThreadsAsync(
            http, "https://dev.azure.com/org", "proj-guid", "repo-guid", prId: 12345);

        Assert.Equal(HttpMethod.Get, captured["method"]);
        var url = captured["url"]?.ToString() ?? "";
        Assert.Contains("/proj-guid/_apis/git/repositories/repo-guid/pullRequests/12345/threads", url);
        Assert.Contains("api-version=7.1-preview.1", url);
    }

    [Fact]
    public async Task ListThreadsAsync_parses_threads_with_comments_file_path_and_line()
    {
        const string json = """
        {
          "value": [
            {
              "id": 101,
              "status": "active",
              "threadContext": { "filePath": "/src/Foo.cs", "rightFileStart": { "line": 42 } },
              "comments": [
                { "id": 1, "author": { "displayName": "Alice" }, "publishedDate": "2026-07-13T10:00:00Z", "content": "Fix this" }
              ]
            },
            {
              "id": 202,
              "status": "fixed",
              "comments": [
                { "id": 9, "author": { "displayName": "Bob" }, "publishedDate": "2026-07-12T09:00:00Z", "content": "Looks good" }
              ]
            }
          ]
        }
        """;
        var handler = new StubHttpHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        });
        using var http = new HttpClient(handler);

        var result = await AzDevOpsPrClient.ListThreadsAsync(
            http, "https://dev.azure.com/org", "proj", "repo", 1);

        Assert.Equal(0, result.ExitCode);
        Assert.Equal(2, result.Threads.Count);
        Assert.Equal(101, result.Threads[0].Id);
        Assert.Equal("active", result.Threads[0].Status);
        Assert.Equal("/src/Foo.cs", result.Threads[0].FilePath);
        Assert.Equal(42, result.Threads[0].LineNumber);
        Assert.Single(result.Threads[0].Comments);
        Assert.Equal("Alice", result.Threads[0].Comments[0].Author);
        Assert.Equal("Fix this", result.Threads[0].Comments[0].Content);
        Assert.Equal(202, result.Threads[1].Id);
        Assert.Null(result.Threads[1].FilePath);
        Assert.Null(result.Threads[1].LineNumber);
    }

    [Fact]
    public async Task ListThreadsAsync_returns_exit_code_1_on_404()
    {
        var handler = new StubHttpHandler(_ => new HttpResponseMessage(HttpStatusCode.NotFound));
        using var http = new HttpClient(handler);

        var result = await AzDevOpsPrClient.ListThreadsAsync(
            http, "https://dev.azure.com/org", "proj", "repo", 9999);

        Assert.Equal(1, result.ExitCode);
        Assert.Empty(result.Threads);
    }

    [Fact]
    public async Task ListThreadsAsync_returns_exit_code_2_on_401()
    {
        var handler = new StubHttpHandler(_ => new HttpResponseMessage(HttpStatusCode.Unauthorized)
        {
            Content = new StringContent("auth failed", Encoding.UTF8, "text/plain")
        });
        using var http = new HttpClient(handler);

        var result = await AzDevOpsPrClient.ListThreadsAsync(
            http, "https://dev.azure.com/org", "proj", "repo", 1);

        Assert.Equal(2, result.ExitCode);
        Assert.NotNull(result.Error);
    }
}
