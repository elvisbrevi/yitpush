using System.Text;
using System.Text.Json;

namespace YitPush;

public sealed record AttachFileResult(int ExitCode, string? AttachmentUrl, string? Error);

internal static class AzDevOpsAttachmentClient
{
    public static async Task<string?> UploadAttachmentAsync(
        HttpClient http, string orgUrl, string project, string filePath)
    {
        var fileName = Path.GetFileName(filePath);
        var url = $"{orgUrl}/{Uri.EscapeDataString(project)}/_apis/wit/attachments" +
                  $"?fileName={Uri.EscapeDataString(fileName)}&api-version=7.1-preview";

        await using var fs = File.OpenRead(filePath);
        using var content = new StreamContent(fs);
        content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");

        using var response = await http.PostAsync(url, content);
        if (!response.IsSuccessStatusCode)
            return null;

        var body = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);
        return doc.RootElement.TryGetProperty("url", out var u) ? u.GetString() : null;
    }

    public static async Task<bool> AddAttachmentRelationAsync(
        HttpClient http, string orgUrl, string workItemId,
        string attachmentUrl, string? comment)
    {
        var url = $"{orgUrl}/_apis/wit/workitems/{workItemId}?api-version=7.0";

        var relation = new Dictionary<string, object>
        {
            ["rel"] = "AttachedFile",
            ["url"] = attachmentUrl,
            ["attributes"] = new Dictionary<string, object>
            {
                ["comment"] = comment ?? "Attached via yp"
            }
        };

        var patchOp = new[]
        {
            new Dictionary<string, object>
            {
                ["op"] = "add",
                ["path"] = "/relations/-",
                ["value"] = relation
            }
        };

        var json = JsonSerializer.Serialize(patchOp);
        using var content = new StringContent(json, Encoding.UTF8, "application/json-patch+json");
        using var response = await http.PatchAsync(url, content);
        return response.IsSuccessStatusCode;
    }

    public static async Task<AttachFileResult> AttachFileToWorkItemAsync(
        HttpClient http, string orgUrl, string project, string workItemId,
        string filePath, string? comment)
    {
        if (!File.Exists(filePath))
            return new AttachFileResult(2, null, $"File not found: {filePath}");

        try
        {
            var attachmentUrl = await UploadAttachmentAsync(http, orgUrl, project, filePath);
            if (attachmentUrl == null)
                return new AttachFileResult(2, null, "Upload failed: Azure DevOps did not return an attachment URL.");

            var ok = await AddAttachmentRelationAsync(http, orgUrl, workItemId, attachmentUrl, comment);
            if (!ok)
                return new AttachFileResult(2, attachmentUrl, "Relation PATCH failed: could not link the attachment to the work item.");

            return new AttachFileResult(0, attachmentUrl, null);
        }
        catch (Exception ex)
        {
            return new AttachFileResult(2, null, ex.Message);
        }
    }
}
