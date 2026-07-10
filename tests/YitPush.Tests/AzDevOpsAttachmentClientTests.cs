using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using YitPush;

namespace YitPush.Tests;

public class AzDevOpsAttachmentClientTests
{
    private static string CreateTempFile(string contents, string fileName)
    {
        var path = Path.Combine(Path.GetTempPath(), fileName);
        File.WriteAllText(path, contents);
        return path;
    }

    [Fact]
    public async Task UploadAttachmentAsync_sends_POST_to_attachments_endpoint_with_octet_stream()
    {
        var captured = new Dictionary<string, object?>();
        var handler = new StubHttpHandler(req =>
        {
            captured["method"] = req.Method;
            captured["url"] = req.RequestUri?.ToString();
            captured["contentType"] = req.Content?.Headers.ContentType?.ToString();
            return new HttpResponseMessage(HttpStatusCode.Created)
            {
                Content = new StringContent(
                    """{"url":"https://dev.azure.com/org/_apis/wit/attachments/abc123"}""",
                    Encoding.UTF8, "application/json")
            };
        });
        using var http = new HttpClient(handler);

        var path = CreateTempFile("hello world", "screenshot.png");
        try
        {
            var url = await AzDevOpsAttachmentClient.UploadAttachmentAsync(
                http, "https://dev.azure.com/org", "My Project", path);

            Assert.Equal(HttpMethod.Post, captured["method"]);
            Assert.Contains("/My Project/_apis/wit/attachments", captured["url"]?.ToString() ?? "");
            Assert.Contains("fileName=screenshot.png", captured["url"]?.ToString() ?? "");
            Assert.Contains("api-version=7.1-preview", captured["url"]?.ToString() ?? "");
            Assert.Equal("application/octet-stream", captured["contentType"]);
            Assert.Equal("https://dev.azure.com/org/_apis/wit/attachments/abc123", url);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task UploadAttachmentAsync_returns_null_on_error()
    {
        var handler = new StubHttpHandler(_ => new HttpResponseMessage(HttpStatusCode.Unauthorized)
        {
            Content = new StringContent("TF400813: anonymous", Encoding.UTF8, "text/plain")
        });
        using var http = new HttpClient(handler);

        var path = CreateTempFile("x", "x.txt");
        try
        {
            var url = await AzDevOpsAttachmentClient.UploadAttachmentAsync(
                http, "https://dev.azure.com/org", "P", path);
            Assert.Null(url);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task AddAttachmentRelationAsync_sends_PATCH_with_add_to_relations()
    {
        var captured = new Dictionary<string, object?>();
        var handler = new StubHttpHandler(req =>
        {
            captured["method"] = req.Method;
            captured["url"] = req.RequestUri?.ToString();
            var bodyTask = req.Content?.ReadAsStringAsync();
            captured["body"] = bodyTask?.Result;
            captured["contentType"] = req.Content?.Headers.ContentType?.ToString();
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{}", Encoding.UTF8, "application/json")
            };
        });
        using var http = new HttpClient(handler);

        var ok = await AzDevOpsAttachmentClient.AddAttachmentRelationAsync(
            http, "https://dev.azure.com/org", "22427",
            "https://dev.azure.com/org/_apis/wit/attachments/abc123",
            "Curl caso feliz folio 3535303");

        Assert.True(ok);
        Assert.Equal(HttpMethod.Patch, captured["method"]);
        Assert.Contains("/_apis/wit/workitems/22427", captured["url"]?.ToString() ?? "");
        var body = captured["body"]?.ToString() ?? "";
        Assert.Contains("\"op\":\"add\"", body);
        Assert.Contains("\"/relations/-\"", body);
        Assert.Contains("AttachedFile", body);
        Assert.Contains("abc123", body);
        Assert.Contains("Curl caso feliz folio 3535303", body);
    }

    [Fact]
    public async Task AddAttachmentRelationAsync_returns_false_on_error()
    {
        var handler = new StubHttpHandler(_ => new HttpResponseMessage(HttpStatusCode.Conflict)
        {
            Content = new StringContent("conflict", Encoding.UTF8, "text/plain")
        });
        using var http = new HttpClient(handler);

        var ok = await AzDevOpsAttachmentClient.AddAttachmentRelationAsync(
            http, "https://dev.azure.com/org", "1", "https://x/y", null);

        Assert.False(ok);
    }

    [Fact]
    public async Task AttachFileToWorkItemAsync_orchestrates_upload_then_relation_and_returns_0()
    {
        var responses = new Queue<HttpResponseMessage>(new[]
        {
            new HttpResponseMessage(HttpStatusCode.Created)
            {
                Content = new StringContent(
                    """{"url":"https://dev.azure.com/org/_apis/wit/attachments/abc123"}""",
                    Encoding.UTF8, "application/json")
            },
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{}", Encoding.UTF8, "application/json")
            }
        });
        var handler = new StubHttpHandler(_ => responses.Dequeue());
        using var http = new HttpClient(handler);

        var path = CreateTempFile("data", "log.txt");
        try
        {
            var result = await AzDevOpsAttachmentClient.AttachFileToWorkItemAsync(
                http, "https://dev.azure.com/org", "My Project", "22427", path, null);

            Assert.Equal(0, result.ExitCode);
            Assert.Equal("https://dev.azure.com/org/_apis/wit/attachments/abc123", result.AttachmentUrl);
            Assert.Equal(2, handler.Requests.Count);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task AttachFileToWorkItemAsync_returns_2_when_upload_fails()
    {
        var handler = new StubHttpHandler(_ => new HttpResponseMessage(HttpStatusCode.Unauthorized)
        {
            Content = new StringContent("auth", Encoding.UTF8, "text/plain")
        });
        using var http = new HttpClient(handler);

        var path = CreateTempFile("x", "x.txt");
        try
        {
            var result = await AzDevOpsAttachmentClient.AttachFileToWorkItemAsync(
                http, "https://dev.azure.com/org", "P", "1", path, null);

            Assert.Equal(2, result.ExitCode);
            Assert.Null(result.AttachmentUrl);
            Assert.Single(handler.Requests);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task AttachFileToWorkItemAsync_returns_2_when_relation_patch_fails()
    {
        var responses = new Queue<HttpResponseMessage>(new[]
        {
            new HttpResponseMessage(HttpStatusCode.Created)
            {
                Content = new StringContent(
                    """{"url":"https://dev.azure.com/org/_apis/wit/attachments/abc123"}""",
                    Encoding.UTF8, "application/json")
            },
            new HttpResponseMessage(HttpStatusCode.InternalServerError)
            {
                Content = new StringContent("oops", Encoding.UTF8, "text/plain")
            }
        });
        var handler = new StubHttpHandler(_ => responses.Dequeue());
        using var http = new HttpClient(handler);

        var path = CreateTempFile("x", "x.txt");
        try
        {
            var result = await AzDevOpsAttachmentClient.AttachFileToWorkItemAsync(
                http, "https://dev.azure.com/org", "P", "1", path, null);

            Assert.Equal(2, result.ExitCode);
            Assert.NotNull(result.Error);
            Assert.Equal(2, handler.Requests.Count);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task AttachFileToWorkItemAsync_returns_2_when_file_does_not_exist()
    {
        var handler = new StubHttpHandler(_ =>
            throw new InvalidOperationException("Handler should not be called for missing file"));
        using var http = new HttpClient(handler);

        var result = await AzDevOpsAttachmentClient.AttachFileToWorkItemAsync(
            http, "https://dev.azure.com/org", "P", "1",
            "/tmp/this-file-definitely-does-not-exist-xyz-12345.bin", null);

        Assert.Equal(2, result.ExitCode);
        Assert.NotNull(result.Error);
        Assert.Contains("not found", result.Error, StringComparison.OrdinalIgnoreCase);
    }
}
