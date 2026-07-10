using System.Text.Json;

namespace YitPush;

internal abstract record IdentityResolutionResult
{
    public sealed record SingleMatch(TaskUpdateOperationsBuilder.IdentityMatch Match) : IdentityResolutionResult;
    public sealed record MultipleMatches(IReadOnlyList<TaskUpdateOperationsBuilder.IdentityMatch> Matches) : IdentityResolutionResult;
    public sealed record NotFound : IdentityResolutionResult;
    public sealed record Clear : IdentityResolutionResult;
}

internal class AzDevOpsIdentityResolver
{
    private readonly HttpClient _http;
    private readonly Dictionary<string, IdentityResolutionResult> _cache = new(StringComparer.OrdinalIgnoreCase);

    public AzDevOpsIdentityResolver(HttpClient http)
    {
        _http = http;
    }

    public async Task<IdentityResolutionResult> ResolveAsync(
        string orgUrl, string input, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return new IdentityResolutionResult.Clear();
        }

        var key = $"{orgUrl}|{input.Trim()}";
        if (_cache.TryGetValue(key, out var cached)) return cached;

        var result = await QueryAsync(orgUrl, input.Trim(), ct);
        _cache[key] = result;
        return result;
    }

    private async Task<IdentityResolutionResult> QueryAsync(
        string orgUrl, string input, CancellationToken ct)
    {
        var url = $"{orgUrl}/_apis/identities?searchFilter=General&filterValue={Uri.EscapeDataString(input)}&api-version=7.0";

        try
        {
            using var response = await _http.GetAsync(url, ct);
            if (!response.IsSuccessStatusCode)
            {
                return await TryGraphFallbackAsync(orgUrl, input, ct);
            }

            var json = await response.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(json);

            if (!doc.RootElement.TryGetProperty("value", out var value) || value.ValueKind != JsonValueKind.Array)
            {
                return new IdentityResolutionResult.NotFound();
            }

            var matches = new List<TaskUpdateOperationsBuilder.IdentityMatch>();
            foreach (var item in value.EnumerateArray())
            {
                if (item.TryGetProperty("id", out var idEl) && idEl.GetGuid() is { } id &&
                    item.TryGetProperty("displayName", out var dnEl) && dnEl.GetString() is { } displayName &&
                    item.TryGetProperty("uniqueName", out var unEl) && unEl.GetString() is { } uniqueName)
                {
                    matches.Add(new TaskUpdateOperationsBuilder.IdentityMatch(
                        id.ToString(), displayName, uniqueName));
                }
            }

            return matches.Count switch
            {
                0 => await TryGraphFallbackAsync(orgUrl, input, ct),
                1 => new IdentityResolutionResult.SingleMatch(matches[0]),
                _ => new IdentityResolutionResult.MultipleMatches(matches)
            };
        }
        catch
        {
            return new IdentityResolutionResult.NotFound();
        }
    }

    private async Task<IdentityResolutionResult> TryGraphFallbackAsync(
        string orgUrl, string input, CancellationToken ct)
    {
        var url = $"{orgUrl}/_apis/graph/users?api-version=7.1-preview.1";

        try
        {
            using var response = await _http.GetAsync(url, ct);
            if (!response.IsSuccessStatusCode) return new IdentityResolutionResult.NotFound();

            var json = await response.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(json);

            if (!doc.RootElement.TryGetProperty("value", out var value) || value.ValueKind != JsonValueKind.Array)
            {
                return new IdentityResolutionResult.NotFound();
            }

            var matches = new List<TaskUpdateOperationsBuilder.IdentityMatch>();
            foreach (var item in value.EnumerateArray())
            {
                if (item.TryGetProperty("displayName", out var dnEl) && dnEl.GetString() is { } displayName &&
                    item.TryGetProperty("principalName", out var pnEl) && pnEl.GetString() is { } principalName)
                {
                    if (string.Equals(principalName, input, StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(displayName, input, StringComparison.OrdinalIgnoreCase))
                    {
                        var id = item.TryGetProperty("originId", out var idEl) && idEl.ValueKind == JsonValueKind.String
                            ? idEl.GetString()!
                            : Guid.Empty.ToString();
                        matches.Add(new TaskUpdateOperationsBuilder.IdentityMatch(
                            id, displayName, principalName));
                    }
                }
            }

            return matches.Count switch
            {
                0 => new IdentityResolutionResult.NotFound(),
                1 => new IdentityResolutionResult.SingleMatch(matches[0]),
                _ => new IdentityResolutionResult.MultipleMatches(matches)
            };
        }
        catch
        {
            return new IdentityResolutionResult.NotFound();
        }
    }
}
