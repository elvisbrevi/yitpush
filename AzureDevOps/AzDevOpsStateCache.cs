using System.Text.Json;

namespace YitPush;

internal class AzDevOpsStateCache
{
    private readonly HttpClient _http;
    private readonly string _cachePath;
    private readonly Dictionary<string, HashSet<string>> _inMemory = new(StringComparer.OrdinalIgnoreCase);

    public AzDevOpsStateCache(HttpClient http)
        : this(http, DefaultCachePath())
    {
    }

    public AzDevOpsStateCache(HttpClient http, string cachePath)
    {
        _http = http;
        _cachePath = cachePath;
    }

    public async Task<HashSet<string>> GetValidStatesAsync(
        string orgUrl, string project, string workItemType)
    {
        var key = BuildCacheKey(orgUrl, project, workItemType);
        if (_inMemory.TryGetValue(key, out var hit)) return hit;

        var states = await FetchFromApiAsync(orgUrl, project, workItemType);
        // Fail open: on API error we cache an empty set so the next call short-circuits,
        // but IsLikelyValidStateAsync still returns true for any input.
        _inMemory[key] = states;
        return states;
    }

    public async Task<bool> IsLikelyValidStateAsync(
        string orgUrl, string project, string workItemType, string state)
    {
        try
        {
            var states = await GetValidStatesAsync(orgUrl, project, workItemType);
            if (states.Count == 0) return true; // fail open
            return states.Contains(state);
        }
        catch
        {
            return true; // fail open — never block the PATCH
        }
    }

    private async Task<HashSet<string>> FetchFromApiAsync(
        string orgUrl, string project, string workItemType)
    {
        var states = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var url = $"{orgUrl}/{Uri.EscapeDataString(project)}/_apis/wit/workitemtypes/{Uri.EscapeDataString(workItemType)}/states?api-version=7.0";

        try
        {
            using var response = await _http.GetAsync(url);
            if (!response.IsSuccessStatusCode) return states;

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("value", out var value)) return states;

            foreach (var state in value.EnumerateArray())
            {
                if (state.TryGetProperty("name", out var nameEl) && nameEl.GetString() is { } name)
                {
                    states.Add(name);
                }
            }
        }
        catch
        {
            return states; // fail open
        }

        return states;
    }

    private static string BuildCacheKey(string orgUrl, string project, string workItemType)
        => $"{orgUrl}|{project}|{workItemType}";

    private static string DefaultCachePath()
        => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".yitpush", "state-cache.json");
}
