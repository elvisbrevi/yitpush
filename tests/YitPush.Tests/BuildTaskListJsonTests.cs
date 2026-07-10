using System.Text.Json;
using YitPush;

namespace YitPush.Tests;

public class BuildTaskListJsonTests
{
    [Fact]
    public void BuildTaskListJson_wraps_tasks_under_value_key_to_match_jq_pipe_pattern()
    {
        var tasks = new List<(string Id, string Title, string State)>
        {
            ("67890", "Implementar login", "Doing"),
            ("67891", "Tests unitarios", "Done"),
        };

        var result = Program.BuildTaskListJson("12345", tasks);

        Assert.True(result.ContainsKey("value"));
        var list = Assert.IsAssignableFrom<IEnumerable<object?>>(result["value"]).ToList();
        Assert.Equal(2, list.Count);
    }

    [Fact]
    public void BuildTaskListJson_includes_huId_for_context()
    {
        var result = Program.BuildTaskListJson("12345", Array.Empty<(string, string, string)>());

        Assert.Equal("12345", result["huId"]);
    }

    [Fact]
    public void BuildTaskListJson_emits_each_task_with_id_title_and_state()
    {
        var tasks = new List<(string Id, string Title, string State)>
        {
            ("99", "Setup CI", "Active")
        };

        var result = Program.BuildTaskListJson("12345", tasks);
        var list = Assert.IsAssignableFrom<IEnumerable<object?>>(result["value"]).ToList();
        var first = (IDictionary<string, object?>)list[0]!;

        Assert.Equal("99", first["id"]);
        Assert.Equal("Setup CI", first["title"]);
        Assert.Equal("Active", first["state"]);
    }

    [Fact]
    public void BuildTaskListJson_emits_empty_value_array_when_no_tasks()
    {
        var result = Program.BuildTaskListJson("12345", Array.Empty<(string, string, string)>());

        var list = Assert.IsAssignableFrom<IEnumerable<object?>>(result["value"]).ToList();
        Assert.Empty(list);
    }

    [Fact]
    public void BuildTaskListJson_round_trips_through_System_Text_Json_with_expected_shape()
    {
        var tasks = new List<(string Id, string Title, string State)>
        {
            ("1", "A", "Done"),
            ("2", "B", "Doing"),
        };
        var result = Program.BuildTaskListJson("12345", tasks);

        var json = JsonSerializer.Serialize(result);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.Equal(2, root.GetProperty("value").GetArrayLength());
        Assert.Equal("12345", root.GetProperty("huId").GetString());
        Assert.Equal("1", root.GetProperty("value")[0].GetProperty("id").GetString());
        Assert.Equal("Doing", root.GetProperty("value")[1].GetProperty("state").GetString());
    }
}
