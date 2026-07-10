using System.Text.Json;

namespace YitPush;

partial class Program
{
    // ─── JSON helpers ─────────────────────────────────────────────────────────

    internal static Dictionary<string, object?> FlattenWorkItem(JsonElement workItemRoot)
    {
        var flat = new Dictionary<string, object?>();

        // id at the root (or System.Id as a fallback)
        var id = workItemRoot.TryGetProperty("id", out var idProp)
            ? idProp.ToString()
            : null;
        if (id == null && workItemRoot.TryGetProperty("fields", out var f0)
            && f0.TryGetProperty("System.Id", out var fId))
        {
            id = fId.ToString();
        }
        if (id != null) flat["id"] = id;

        if (workItemRoot.TryGetProperty("fields", out var fields)
            && fields.ValueKind == JsonValueKind.Object)
        {
            foreach (var field in fields.EnumerateObject())
            {
                // System.Id is redundant with the root id (which we already stored as a string)
                if (field.Name == "System.Id") continue;

                var key = NormalizeKey(field.Name);
                flat[key] = ExtractFieldValue(field.Name, field.Value);
            }
        }

        if (workItemRoot.TryGetProperty("relations", out var relations)
            && relations.ValueKind == JsonValueKind.Array)
        {
            var list = new List<object?>();
            foreach (var rel in relations.EnumerateArray())
            {
                var relType = rel.TryGetProperty("rel", out var rp) ? rp.GetString() ?? "" : "";
                var url = rel.TryGetProperty("url", out var up) ? up.GetString() ?? "" : "";

                if (string.IsNullOrEmpty(url)) continue;

                var displayType = relType switch
                {
                    "System.LinkTypes.Hierarchy-Forward" => "Child Task",
                    "System.LinkTypes.Hierarchy-Reverse" => "Parent HU",
                    "ArtifactLink" => "Branch/Commit/PR",
                    "Hyperlink" => "External Link",
                    _ => relType
                };

                var target = url;
                if (relType.Contains("Hierarchy", StringComparison.OrdinalIgnoreCase))
                {
                    var lastSlash = url.LastIndexOf('/');
                    if (lastSlash >= 0) target = "#" + url[(lastSlash + 1)..];
                }

                list.Add(new Dictionary<string, object?>
                {
                    ["rel"] = relType,
                    ["type"] = displayType,
                    ["url"] = url,
                    ["target"] = target
                });
            }
            flat["relations"] = list;
        }

        return flat;
    }

    internal static Dictionary<string, object?> BuildTaskListJson(
        string huId,
        IEnumerable<(string Id, string Title, string State)> tasks)
    {
        var value = tasks.Select(t => (IDictionary<string, object?>)new Dictionary<string, object?>
        {
            ["id"] = t.Id,
            ["title"] = t.Title,
            ["state"] = t.State
        }).Cast<object?>().ToList();

        return new Dictionary<string, object?>
        {
            ["huId"] = huId,
            ["value"] = value
        };
    }

    private static string NormalizeKey(string refName) => refName switch
    {
        "System.Id" => "id",
        "System.WorkItemType" => "type",
        "System.Title" => "title",
        "System.State" => "state",
        "System.AssignedTo" => "assignedTo",
        "System.CreatedDate" => "createdDate",
        "System.AreaPath" => "areaPath",
        "System.IterationPath" => "iterationPath",
        "System.Description" => "description",
        "Custom.EsfuerzoEstimadoHH" => "effort",
        "Custom.EsfuerzoRealHH" => "effortReal",
        "Microsoft.VSTS.Scheduling.RemainingWork" => "remaining",
        "Custom.Mes" => "month",
        "Custom.URLCommit" => "urlCommit",
        _ => refName
    };

    private static object? ExtractFieldValue(string refName, JsonElement value)
    {
        // System.AssignedTo is an object { displayName, uniqueName } — flatten to displayName
        if (refName == "System.AssignedTo" && value.ValueKind == JsonValueKind.Object)
        {
            if (value.TryGetProperty("displayName", out var dn)) return dn.GetString();
            return value.ToString();
        }
        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Number => value.TryGetInt64(out var l) ? (object)l : value.GetDouble(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null => null,
            _ => value.ToString()
        };
    }
}
