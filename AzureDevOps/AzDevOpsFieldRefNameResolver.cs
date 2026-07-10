using System.Text.Json;
using System.Text.Json.Serialization;

namespace YitPush;

internal class AzDevOpsFieldRefNameResolver
{
    private const int CacheTtlHours = 24;
    private const string OrgUrlKey = "org";

    private readonly HttpClient _http;
    private readonly string _cachePath;
    private readonly Dictionary<string, Dictionary<string, string>> _inMemory = new();

    public AzDevOpsFieldRefNameResolver(HttpClient http)
        : this(http, DefaultCachePath())
    {
    }

    public AzDevOpsFieldRefNameResolver(HttpClient http, string cachePath)
    {
        _http = http;
        _cachePath = cachePath;
    }

    public async Task<string?> ResolveByDisplayNameAsync(
        string orgUrl, string project, string workItemType, string displayName)
    {
        var fields = await GetFieldsAsync(orgUrl, project, workItemType);
        return fields.TryGetValue(displayName, out var refName) ? refName : null;
    }

    public async Task RefreshAsync(string orgUrl, string project, string workItemType)
    {
        var key = BuildCacheKey(orgUrl, project, workItemType);
        _inMemory.Remove(key);
        RemoveFromDisk(key);
        await GetFieldsAsync(orgUrl, project, workItemType);
    }

    private void RemoveFromDisk(string key)
    {
        if (!File.Exists(_cachePath)) return;
        try
        {
            var json = File.ReadAllText(_cachePath);
            var cache = JsonSerializer.Deserialize<FieldCache>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (cache?.Entries == null || !cache.Entries.Remove(key)) return;

            File.WriteAllText(_cachePath, JsonSerializer.Serialize(cache,
                new JsonSerializerOptions { WriteIndented = true }));
        }
        catch { /* best-effort */ }
    }

    private async Task<Dictionary<string, string>> GetFieldsAsync(
        string orgUrl, string project, string workItemType)
    {
        var key = BuildCacheKey(orgUrl, project, workItemType);
        if (_inMemory.TryGetValue(key, out var hit)) return hit;

        if (TryLoadFromDisk(key, out var fromDisk)) return fromDisk;

        var (fetched, fromApi) = await FetchFromApiAsync(orgUrl, project, workItemType);
        if (fromApi)
        {
            _inMemory[key] = fetched;
            PersistCache();
        }
        else
        {
            // API error or empty payload (e.g. TF51535): do not cache, let the next
            // resolution try the API again so transient issues / typos self-heal.
            _inMemory.Remove(key);
            RemoveFromDisk(key);
        }
        return fetched;
    }

    private async Task<(Dictionary<string, string> Fields, bool FromApi)> FetchFromApiAsync(
        string orgUrl, string project, string workItemType)
    {
        var fields = new Dictionary<string, string>(StringComparer.Ordinal);
        var url = $"{orgUrl}/{Uri.EscapeDataString(project)}/_apis/wit/workitemtypes/{Uri.EscapeDataString(workItemType)}/fields?api-version=7.0";

        try
        {
            using var response = await _http.GetAsync(url);
            if (!response.IsSuccessStatusCode) return (fields, false);

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);

            if (!doc.RootElement.TryGetProperty("value", out var value)) return (fields, false);

            foreach (var field in value.EnumerateArray())
            {
                if (field.TryGetProperty("name", out var nameEl) &&
                    field.TryGetProperty("referenceName", out var refEl) &&
                    nameEl.GetString() is { } name &&
                    refEl.GetString() is { } refName)
                {
                    fields[name] = refName;
                }
            }
        }
        catch
        {
            return (fields, false);
        }

        return (fields, true);
    }

    private bool TryLoadFromDisk(string key, out Dictionary<string, string> fields)
    {
        fields = new Dictionary<string, string>(StringComparer.Ordinal);
        if (!File.Exists(_cachePath)) return false;

        try
        {
            var json = File.ReadAllText(_cachePath);
            var cache = JsonSerializer.Deserialize<FieldCache>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (cache?.Entries == null) return false;
            if (!cache.Entries.TryGetValue(key, out var entry)) return false;
            if ((DateTime.UtcNow - entry.LastCheck).TotalHours >= CacheTtlHours) return false;

            foreach (var (name, refName) in entry.Fields) fields[name] = refName;
            _inMemory[key] = fields;
            return fields.Count > 0;
        }
        catch
        {
            return false;
        }
    }

    private void PersistCache()
    {
        try
        {
            var dir = Path.GetDirectoryName(_cachePath);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

            var cache = new FieldCache();
            if (File.Exists(_cachePath))
            {
                try
                {
                    var existing = JsonSerializer.Deserialize<FieldCache>(File.ReadAllText(_cachePath),
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    if (existing?.Entries != null) cache.Entries = existing.Entries;
                }
                catch { }
            }

            foreach (var (key, fields) in _inMemory)
            {
                cache.Entries[key] = new FieldCacheEntry
                {
                    LastCheck = DateTime.UtcNow,
                    Fields = new Dictionary<string, string>(fields)
                };
            }

            File.WriteAllText(_cachePath, JsonSerializer.Serialize(cache,
                new JsonSerializerOptions { WriteIndented = true }));
        }
        catch { /* cache write is non-critical */ }
    }

    private static string BuildCacheKey(string orgUrl, string project, string workItemType)
        => $"{orgUrl}|{project}|{workItemType}";

    private static string DefaultCachePath()
        => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".yitpush", "field-cache.json");
}

internal class FieldCacheEntry
{
    [JsonPropertyName("lastCheck")]
    public DateTime LastCheck { get; set; }

    [JsonPropertyName("fields")]
    public Dictionary<string, string> Fields { get; set; } = new();
}

internal class FieldCache
{
    [JsonPropertyName("entries")]
    public Dictionary<string, FieldCacheEntry> Entries { get; set; } = new();
}
