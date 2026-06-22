using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using YitPush;

namespace YitPush.Tests;

public class AzDevOpsCommentClientTests
{
    [Fact]
    public async Task PostDiscussionCommentAsync_sends_POST_to_comments_endpoint()
    {
        var captured = new Dictionary<string, object?>();
        var handler = new StubHttpHandler(req =>
        {
            captured["method"] = req.Method;
            captured["url"] = req.RequestUri?.ToString();
            captured["contentType"] = req.Content?.Headers.ContentType?.ToString();
            var bodyTask = req.Content?.ReadAsStringAsync();
            captured["body"] = bodyTask?.Result;
            return new HttpResponseMessage(HttpStatusCode.Created)
            {
                Content = new StringContent("{}", Encoding.UTF8, "application/json")
            };
        });
        using var http = new HttpClient(handler);

        var ok = await AzDevOpsCommentClient.PostDiscussionCommentAsync(
            http, "https://dev.azure.com/org", "My Project", "12345", "Hola mundo");

        Assert.True(ok);
        Assert.Equal(HttpMethod.Post, captured["method"]);
        // .NET normalizes the Uri and decodes %20; verify the project is in the path
        Assert.Contains("/My Project/", captured["url"]?.ToString() ?? "");
        Assert.Contains("application/json", captured["contentType"]?.ToString() ?? "");
        Assert.Contains("Hola mundo", captured["body"]?.ToString() ?? "");
    }

    [Fact]
    public async Task PostDiscussionCommentAsync_returns_false_on_error()
    {
        var handler = new StubHttpHandler(_ => new HttpResponseMessage(HttpStatusCode.Unauthorized)
        {
            Content = new StringContent("{}", Encoding.UTF8, "application/json")
        });
        using var http = new HttpClient(handler);

        var ok = await AzDevOpsCommentClient.PostDiscussionCommentAsync(
            http, "https://dev.azure.com/org", "P", "1", "x");

        Assert.False(ok);
    }
}
