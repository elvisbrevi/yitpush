using System.Text.Json;

namespace YitPush;

internal static class TaskUpdatePreFlight
{
    public sealed record PreFlightRequest(
        HttpClient Http,
        string OrgUrl,
        string WorkItemId,
        string? State,
        string? EvidenceFlagValue,
        string? EvidenceRefName,
        string EvidenceDisplayName);

    public abstract record PreFlightOutcome;
    public sealed record PreFlightPassed : PreFlightOutcome;
    public sealed record PreFlightSkipped(string Reason) : PreFlightOutcome;
    public sealed record PreFlightFailed(string Message) : PreFlightOutcome;

    public static string BuildMissingEvidenceMessage(string displayName, string? refName)
    {
        var refnamePart = !string.IsNullOrEmpty(refName) ? $" (refname {refName})" : "";
        return
            $"❌ Missing required field for Done transition: {displayName}{refnamePart}. " +
            $"Re-run with --evidence \"<text>\".";
    }

    public static async Task<PreFlightOutcome> RunAsync(PreFlightRequest request)
    {
        if (!string.Equals(request.State, "Done", StringComparison.OrdinalIgnoreCase))
        {
            return new PreFlightPassed();
        }

        if (!string.IsNullOrEmpty(request.EvidenceFlagValue))
        {
            return new PreFlightPassed();
        }

        if (string.IsNullOrEmpty(request.EvidenceRefName))
        {
            return new PreFlightSkipped("evidence refname unresolved");
        }

        try
        {
            var url = $"{request.OrgUrl}/_apis/wit/workitems/{Uri.EscapeDataString(request.WorkItemId)}?api-version=7.0";
            using var response = await request.Http.GetAsync(url);
            if (!response.IsSuccessStatusCode)
            {
                return new PreFlightSkipped($"GET returned {(int)response.StatusCode}");
            }

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("fields", out var fields))
            {
                return new PreFlightSkipped("no fields in response");
            }

            if (fields.TryGetProperty(request.EvidenceRefName, out var evidenceProp))
            {
                var value = evidenceProp.ValueKind == JsonValueKind.String
                    ? evidenceProp.GetString()
                    : evidenceProp.GetRawText();
                if (!string.IsNullOrWhiteSpace(value))
                {
                    return new PreFlightPassed();
                }
            }

            return new PreFlightFailed(
                BuildMissingEvidenceMessage(request.EvidenceDisplayName, request.EvidenceRefName));
        }
        catch
        {
            return new PreFlightSkipped("GET threw");
        }
    }
}
