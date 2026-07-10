using System.Net;

namespace YitPush;

public sealed record DeleteWorkItemResult(int ExitCode, string? Error);

internal static class AzDevOpsDeleteClient
{
    public static async Task<DeleteWorkItemResult> DeleteWorkItemAsync(
        HttpClient http, string orgUrl, string workItemId)
    {
        try
        {
            var url = $"{orgUrl}/_apis/wit/recyclebin/{workItemId}?api-version=7.0";
            using var response = await http.DeleteAsync(url);
            if (response.IsSuccessStatusCode)
                return new DeleteWorkItemResult(0, null);

            if (response.StatusCode == HttpStatusCode.NotFound)
                return new DeleteWorkItemResult(1, null);

            var body = await response.Content.ReadAsStringAsync();
            return new DeleteWorkItemResult(2, string.IsNullOrWhiteSpace(body) ? response.ReasonPhrase : body);
        }
        catch (Exception ex)
        {
            return new DeleteWorkItemResult(2, ex.Message);
        }
    }
}
