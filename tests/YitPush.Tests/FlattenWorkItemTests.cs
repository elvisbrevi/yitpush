using System.Text.Json;
using YitPush;

namespace YitPush.Tests;

public class FlattenWorkItemTests
{
    private const string WorkItemJson = """
    {
      "id": 12345,
      "rev": 3,
      "fields": {
        "System.Id": 12345,
        "System.WorkItemType": "User Story",
        "System.Title": "Implementar login OAuth",
        "System.State": "Active",
        "System.AssignedTo": { "displayName": "Ada Lovelace", "uniqueName": "ada@example.com" },
        "System.CreatedDate": "2025-01-15T10:00:00Z",
        "System.AreaPath": "MyProject\\Auth",
        "System.IterationPath": "MyProject\\Sprint 1",
        "Custom.EsfuerzoEstimadoHH": 8.0,
        "Custom.EsfuerzoRealHH": 3.0,
        "Microsoft.VSTS.Scheduling.RemainingWork": 5.0,
        "Custom.Mes": "Enero",
        "Custom.URLCommit": "https://dev.azure.com/org/proj/_git/repo/commit/abc",
        "System.Description": "<p>Description body</p>",
        "Custom.UnknownField": "secret-value"
      },
      "relations": [
        { "rel": "System.LinkTypes.Hierarchy-Forward", "url": "https://dev.azure.com/org/_apis/wit/workItems/67890", "attributes": { "name": "Child" } }
      ]
    }
    """;

    [Fact]
    public void FlattenWorkItem_exposes_known_fields_at_top_level_with_camel_case_names()
    {
        var root = JsonDocument.Parse(WorkItemJson).RootElement;
        var flat = Program.FlattenWorkItem(root);

        Assert.Equal("12345", flat["id"]);
        Assert.Equal("User Story", flat["type"]);
        Assert.Equal("Implementar login OAuth", flat["title"]);
        Assert.Equal("Active", flat["state"]);
        Assert.Equal("Ada Lovelace", flat["assignedTo"]);
        Assert.Equal("2025-01-15T10:00:00Z", flat["createdDate"]);
        Assert.Equal("MyProject\\Auth", flat["areaPath"]);
        Assert.Equal("MyProject\\Sprint 1", flat["iterationPath"]);
        Assert.Equal("Enero", flat["month"]);
        Assert.Equal("https://dev.azure.com/org/proj/_git/repo/commit/abc", flat["urlCommit"]);
    }

    [Fact]
    public void FlattenWorkItem_does_not_emit_nested_fields_envelope()
    {
        var root = JsonDocument.Parse(WorkItemJson).RootElement;
        var flat = Program.FlattenWorkItem(root);

        Assert.False(flat.ContainsKey("fields"));
    }

    [Fact]
    public void FlattenWorkItem_normalizes_effort_remaining_to_short_names()
    {
        var root = JsonDocument.Parse(WorkItemJson).RootElement;
        var flat = Program.FlattenWorkItem(root);

        Assert.Equal(8.0, Convert.ToDouble(flat["effort"]!));
        Assert.Equal(3.0, Convert.ToDouble(flat["effortReal"]!));
        Assert.Equal(5.0, Convert.ToDouble(flat["remaining"]!));
    }

    [Fact]
    public void FlattenWorkItem_passes_through_unknown_custom_fields_with_their_refname()
    {
        var root = JsonDocument.Parse(WorkItemJson).RootElement;
        var flat = Program.FlattenWorkItem(root);

        Assert.Equal("secret-value", flat["Custom.UnknownField"]);
    }

    [Fact]
    public void FlattenWorkItem_includes_a_relations_array_with_short_target()
    {
        var root = JsonDocument.Parse(WorkItemJson).RootElement;
        var flat = Program.FlattenWorkItem(root);

        Assert.True(flat.TryGetValue("relations", out var rels));
        var list = Assert.IsAssignableFrom<IEnumerable<object?>>(rels).ToList();
        Assert.Single(list);
        var first = (IDictionary<string, object?>)list[0]!;
        Assert.Equal("#67890", first["target"]);
    }

    [Fact]
    public void FlattenWorkItem_handles_minimal_payload_with_only_id()
    {
        var root = JsonDocument.Parse("""{ "id": 99, "fields": { "System.Id": 99 } }""").RootElement;
        var flat = Program.FlattenWorkItem(root);

        Assert.Equal("99", flat["id"]);
        Assert.Single(flat);
    }
}
