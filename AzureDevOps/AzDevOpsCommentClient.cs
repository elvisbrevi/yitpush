using System.Text;
using System.Text.Json;

namespace YitPush;

internal static class AzDevOpsCommentClient
{
    public static async Task<bool> PostDiscussionCommentAsync(
        HttpClient http, string orgUrl, string workItemId, string text)
    {
        var url = $"{orgUrl}/_apis/wit/workItems/{workItemId}/comments?api-version=7.0";
        var body = JsonSerializer.Serialize(new { text });
        using var content = new StringContent(body, Encoding.UTF8, "application/json");

        using var response = await http.PostAsync(url, content);
        return response.IsSuccessStatusCode;
    }
}
