using System.Net;
using System.Text;
using System.Text.Json;

namespace YitPush;

internal static class AzDevOpsPrClient
{
    public sealed record CreatePrRequest(
        string SourceRefName,
        string TargetRefName,
        string Title,
        string Description,
        bool AutoComplete);

    public sealed record CreatePrResult(int ExitCode, string? PullRequestId, string? Error);

    public sealed record ReplyResult(int ExitCode, string? Error);

    public sealed record PrThreadComment(
        [property: System.Text.Json.Serialization.JsonPropertyName("id")] int Id,
        [property: System.Text.Json.Serialization.JsonPropertyName("author")] string Author,
        [property: System.Text.Json.Serialization.JsonPropertyName("date")] string Date,
        [property: System.Text.Json.Serialization.JsonPropertyName("content")] string Content);

    public sealed record PrThread(
        [property: System.Text.Json.Serialization.JsonPropertyName("id")] int Id,
        [property: System.Text.Json.Serialization.JsonPropertyName("status")] string Status,
        [property: System.Text.Json.Serialization.JsonPropertyName("filePath")] string? FilePath,
        [property: System.Text.Json.Serialization.JsonPropertyName("lineNumber")] int? LineNumber,
        [property: System.Text.Json.Serialization.JsonPropertyName("comments")] IReadOnlyList<PrThreadComment> Comments);

    public sealed record ThreadsResult(int ExitCode, IReadOnlyList<PrThread> Threads, string? Error);

    public static async Task<CreatePrResult> CreatePullRequestAsync(
        HttpClient http, string orgUrl, string projectId, string repoId, CreatePrRequest request)
    {
        try
        {
            var url = $"{orgUrl}/{Uri.EscapeDataString(projectId)}/_apis/git/repositories/{repoId}/pullRequests?api-version=7.1-preview.1";

            object body = request.AutoComplete
                ? new
                {
                    sourceRefName = request.SourceRefName,
                    targetRefName = request.TargetRefName,
                    title = request.Title,
                    description = request.Description,
                    completionOptions = new { deleteSourceBranch = true }
                }
                : new
                {
                    sourceRefName = request.SourceRefName,
                    targetRefName = request.TargetRefName,
                    title = request.Title,
                    description = request.Description
                };

            var json = JsonSerializer.Serialize(body);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");

            using var response = await http.PostAsync(url, content);
            if (response.IsSuccessStatusCode)
            {
                var responseBody = await response.Content.ReadAsStringAsync();
                string? prId = null;
                try
                {
                    using var doc = JsonDocument.Parse(responseBody);
                    if (doc.RootElement.TryGetProperty("pullRequestId", out var idProp))
                        prId = idProp.ToString();
                }
                catch
                {
                    // Non-JSON 2xx body — the PR was created, we just couldn't parse the id.
                }
                return new CreatePrResult(0, prId, null);
            }

            if (response.StatusCode == HttpStatusCode.NotFound)
                return new CreatePrResult(1, null, null);

            var errorBody = await response.Content.ReadAsStringAsync();
            return new CreatePrResult(2, null,
                string.IsNullOrWhiteSpace(errorBody) ? response.ReasonPhrase : errorBody);
        }
        catch (Exception ex)
        {
            return new CreatePrResult(2, null, ex.Message);
        }
    }

    public static async Task<ReplyResult> PostThreadCommentAsync(
        HttpClient http, string orgUrl, string projectId, string repoId,
        int prId, int threadId, string body)
    {
        try
        {
            var url = $"{orgUrl}/{Uri.EscapeDataString(projectId)}/_apis/git/repositories/{repoId}/pullRequests/{prId}/threads/{threadId}/comments?api-version=7.1-preview.1";
            var json = JsonSerializer.Serialize(new { content = body });
            using var content = new StringContent(json, Encoding.UTF8, "application/json");

            using var response = await http.PostAsync(url, content);
            if (response.IsSuccessStatusCode)
                return new ReplyResult(0, null);

            if (response.StatusCode == HttpStatusCode.NotFound)
                return new ReplyResult(1, null);

            var errorBody = await response.Content.ReadAsStringAsync();
            return new ReplyResult(2,
                string.IsNullOrWhiteSpace(errorBody) ? response.ReasonPhrase : errorBody);
        }
        catch (Exception ex)
        {
            return new ReplyResult(2, ex.Message);
        }
    }

    public static async Task<ThreadsResult> ListThreadsAsync(
        HttpClient http, string orgUrl, string projectId, string repoId, int prId)
    {
        try
        {
            var url = $"{orgUrl}/{Uri.EscapeDataString(projectId)}/_apis/git/repositories/{repoId}/pullRequests/{prId}/threads?api-version=7.1-preview.1";
            using var response = await http.GetAsync(url);
            if (!response.IsSuccessStatusCode)
            {
                if (response.StatusCode == HttpStatusCode.NotFound)
                    return new ThreadsResult(1, Array.Empty<PrThread>(), null);
                var errorBody = await response.Content.ReadAsStringAsync();
                return new ThreadsResult(2, Array.Empty<PrThread>(),
                    string.IsNullOrWhiteSpace(errorBody) ? response.ReasonPhrase : errorBody);
            }

            var json = await response.Content.ReadAsStringAsync();
            return new ThreadsResult(0, ParseThreadsJson(json), null);
        }
        catch (Exception ex)
        {
            return new ThreadsResult(2, Array.Empty<PrThread>(), ex.Message);
        }
    }

    internal static IReadOnlyList<PrThread> ParseThreadsJson(string json)
    {
        using var doc = JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty("value", out var valueProp) ||
            valueProp.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<PrThread>();
        }

        var threads = new List<PrThread>();
        foreach (var t in valueProp.EnumerateArray())
        {
            int id = t.TryGetProperty("id", out var idProp) ? idProp.GetInt32() : 0;
            string status = t.TryGetProperty("status", out var sProp) ? sProp.GetString() ?? "" : "";

            string? filePath = null;
            int? lineNumber = null;
            if (t.TryGetProperty("threadContext", out var ctx) &&
                ctx.ValueKind == JsonValueKind.Object)
            {
                if (ctx.TryGetProperty("filePath", out var fpProp))
                    filePath = fpProp.GetString();
                if (ctx.TryGetProperty("rightFileStart", out var rfs) &&
                    rfs.ValueKind == JsonValueKind.Object &&
                    rfs.TryGetProperty("line", out var lineProp))
                {
                    lineNumber = lineProp.GetInt32();
                }
            }

            var comments = new List<PrThreadComment>();
            if (t.TryGetProperty("comments", out var commentsProp) &&
                commentsProp.ValueKind == JsonValueKind.Array)
            {
                foreach (var c in commentsProp.EnumerateArray())
                {
                    int cId = c.TryGetProperty("id", out var ci) ? ci.GetInt32() : 0;
                    string author = "";
                    if (c.TryGetProperty("author", out var authorProp) &&
                        authorProp.ValueKind == JsonValueKind.Object &&
                        authorProp.TryGetProperty("displayName", out var dn))
                    {
                        author = dn.GetString() ?? "";
                    }
                    string date = c.TryGetProperty("publishedDate", out var dp) ? dp.GetString() ?? "" : "";
                    string content = c.TryGetProperty("content", out var cp) ? cp.GetString() ?? "" : "";
                    comments.Add(new PrThreadComment(cId, author, date, content));
                }
            }

            threads.Add(new PrThread(id, status, filePath, lineNumber, comments));
        }

        return threads;
    }
}
