using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using YitPush;

namespace YitPush.Tests;

public class AzDevOpsDeleteClientTests
{
    [Fact]
    public async Task DeleteWorkItemAsync_sends_DELETE_to_recyclebin_endpoint()
    {
        var captured = new Dictionary<string, object?>();
        var handler = new StubHttpHandler(req =>
        {
            captured["method"] = req.Method;
            captured["url"] = req.RequestUri?.ToString();
            return new HttpResponseMessage(HttpStatusCode.NoContent);
        });
        using var http = new HttpClient(handler);

        var result = await AzDevOpsDeleteClient.DeleteWorkItemAsync(
            http, "https://dev.azure.com/org", "22428");

        Assert.Equal(HttpMethod.Delete, captured["method"]);
        Assert.Contains("/_apis/wit/recyclebin/22428", captured["url"]?.ToString() ?? "");
        Assert.Contains("api-version=7.0", captured["url"]?.ToString() ?? "");
    }

    [Fact]
    public async Task DeleteWorkItemAsync_returns_zero_on_2xx_success()
    {
        var handler = new StubHttpHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
        using var http = new HttpClient(handler);

        var result = await AzDevOpsDeleteClient.DeleteWorkItemAsync(
            http, "https://dev.azure.com/org", "1");

        Assert.Equal(0, result.ExitCode);
        Assert.Null(result.Error);
    }

    [Fact]
    public async Task DeleteWorkItemAsync_returns_exit_code_1_on_404_already_gone()
    {
        var handler = new StubHttpHandler(_ => new HttpResponseMessage(HttpStatusCode.NotFound)
        {
            Content = new StringContent("{\"message\":\"Work item does not exist.\"}", Encoding.UTF8, "application/json")
        });
        using var http = new HttpClient(handler);

        var result = await AzDevOpsDeleteClient.DeleteWorkItemAsync(
            http, "https://dev.azure.com/org", "99999");

        Assert.Equal(1, result.ExitCode);
    }

    [Fact]
    public async Task DeleteWorkItemAsync_returns_exit_code_2_on_other_error()
    {
        var handler = new StubHttpHandler(_ => new HttpResponseMessage(HttpStatusCode.Unauthorized)
        {
            Content = new StringContent("TF400813: Resource not available for anonymous user.", Encoding.UTF8, "text/plain")
        });
        using var http = new HttpClient(handler);

        var result = await AzDevOpsDeleteClient.DeleteWorkItemAsync(
            http, "https://dev.azure.com/org", "1");

        Assert.Equal(2, result.ExitCode);
        Assert.NotNull(result.Error);
        Assert.Contains("TF400813", result.Error);
    }
}
