using YitPush;

namespace YitPush.Tests;

public class AzureDevOpsFlagParserTests
{
    [Fact]
    public void Parse_sets_title_when_title_flag_provided()
    {
        var result = AzureDevOpsFlagParser.Parse(
            new[] { "task", "update", "--title", "Nuevo título" });

        Assert.Equal("Nuevo título", result.Title);
    }

    [Fact]
    public void Parse_sets_description_when_description_flag_provided()
    {
        var result = AzureDevOpsFlagParser.Parse(
            new[] { "task", "update", "--description", "Nueva descripción" });

        Assert.Equal("Nueva descripción", result.Description);
    }

    [Fact]
    public void Parse_accepts_D_short_flag_for_description()
    {
        var result = AzureDevOpsFlagParser.Parse(
            new[] { "task", "update", "-D", "Corta" });

        Assert.Equal("Corta", result.Description);
    }

    [Fact]
    public void Parse_sets_evidence_when_evidence_flag_provided()
    {
        var result = AzureDevOpsFlagParser.Parse(
            new[] { "task", "update", "--evidence", "curl localhost..." });

        Assert.Equal("curl localhost...", result.Evidence);
    }

    [Fact]
    public void Parse_sets_history_when_history_flag_provided()
    {
        var result = AzureDevOpsFlagParser.Parse(
            new[] { "task", "update", "--history", "Audit log entry" });

        Assert.Equal("Audit log entry", result.History);
    }

    [Fact]
    public void Parse_collects_all_field_flags_into_extras()
    {
        var result = AzureDevOpsFlagParser.Parse(
            new[] { "task", "update", "--field", "System.Tags=foo", "--field", "System.AssignedTo=bar" });

        Assert.Equal(new[] { "System.Tags=foo", "System.AssignedTo=bar" }, result.ExtraFields);
    }

    [Fact]
    public void Parse_sets_Json_true_when_json_flag_provided()
    {
        var result = AzureDevOpsFlagParser.Parse(
            new[] { "hu", "show", "--json" });

        Assert.True(result.Json);
    }

    [Fact]
    public void Parse_sets_Json_false_when_json_flag_missing()
    {
        var result = AzureDevOpsFlagParser.Parse(
            new[] { "hu", "show" });

        Assert.False(result.Json);
    }
}
