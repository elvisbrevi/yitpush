using System.Text.Json;

namespace YitPush;

internal class AzDevOpsFieldRefNameResolver
{
    private readonly HttpClient _http;
    private readonly Dictionary<string, string> _cache = new();

    public AzDevOpsFieldRefNameResolver(HttpClient http)
    {
        _http = http;
    }

    public async Task<string?> ResolveAsync(
        string orgUrl, string project, string workItemType, string displayName)
    {
        var cacheKey = $"{project}|{workItemType}|{displayName}";
        if (_cache.TryGetValue(cacheKey, out var cached)) return cached;

        var url = $"{orgUrl}/{Uri.EscapeDataString(project)}/_apis/wit/workitemtypes/{Uri.EscapeDataString(workItemType)}/fields?api-version=7.0";

        try
        {
            using var response = await _http.GetAsync(url);
            if (!response.IsSuccessStatusCode) return null;

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);

            if (!doc.RootElement.TryGetProperty("value", out var value)) return null;

            foreach (var field in value.EnumerateArray())
            {
                if (field.TryGetProperty("name", out var nameEl) &&
                    field.TryGetProperty("referenceName", out var refEl) &&
                    nameEl.GetString() == displayName)
                {
                    var refName = refEl.GetString();
                    if (refName is not null) _cache[cacheKey] = refName;
                    return refName;
                }
            }
        }
        catch
        {
            return null;
        }

        return null;
    }
}
