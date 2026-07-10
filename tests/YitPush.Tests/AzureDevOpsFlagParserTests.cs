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
    public void Parse_sets_yes_when_yes_flag_provided()
    {
        var result = AzureDevOpsFlagParser.Parse(
            new[] { "task", "delete", "--yes" });

        Assert.True(result.Yes);
    }

    [Fact]
    public void Parse_sets_yes_when_y_short_flag_provided()
    {
        var result = AzureDevOpsFlagParser.Parse(
            new[] { "task", "delete", "-y" });

        Assert.True(result.Yes);
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
    public void Parse_accepts_d_short_flag_for_description()
    {
        var result = AzureDevOpsFlagParser.Parse(
            new[] { "hu", "task", "-d", "Corta" });

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

    [Fact]
    public void Parse_sets_comment_when_c_short_flag_provided()
    {
        var result = AzureDevOpsFlagParser.Parse(
            new[] { "task", "update", "-c", "Comentario" });

        Assert.Equal("Comentario", result.Comment);
    }

    [Fact]
    public void Parse_sets_effort_when_e_short_flag_provided()
    {
        var result = AzureDevOpsFlagParser.Parse(
            new[] { "hu", "task", "Org", "Proj", "123", "-e", "8" });

        Assert.Equal("8", result.Effort);
    }

    [Fact]
    public void Parse_sets_effort_real_when_er_short_flag_provided()
    {
        var result = AzureDevOpsFlagParser.Parse(
            new[] { "task", "update", "Org", "1", "-er", "3" });

        Assert.Equal("3", result.EffortReal);
    }

    [Fact]
    public void Parse_sets_remaining_when_r_short_flag_provided()
    {
        var result = AzureDevOpsFlagParser.Parse(
            new[] { "task", "update", "Org", "1", "-r", "2" });

        Assert.Equal("2", result.Remaining);
    }

    [Fact]
    public void Parse_sets_state_when_s_short_flag_provided()
    {
        var result = AzureDevOpsFlagParser.Parse(
            new[] { "task", "update", "Org", "1", "-s", "Doing" });

        Assert.Equal("Doing", result.State);
    }

    [Fact]
    public void Parse_sets_repo_when_repo_flag_provided()
    {
        var result = AzureDevOpsFlagParser.Parse(
            new[] { "hu", "task", "Org", "Proj", "123", "--repo", "MyRepo" });

        Assert.Equal("MyRepo", result.Repo);
    }

    [Fact]
    public void Parse_sets_branch_when_branch_flag_provided()
    {
        var result = AzureDevOpsFlagParser.Parse(
            new[] { "hu", "task", "Org", "Proj", "123", "--branch", "feature/123" });

        Assert.Equal("feature/123", result.Branch);
    }

    [Fact]
    public void Parse_sets_task_titles_when_t_short_flag_provided()
    {
        var result = AzureDevOpsFlagParser.Parse(
            new[] { "hu", "task", "Org", "Proj", "123", "-t", "A, B, C" });

        Assert.Equal("A, B, C", result.TaskTitles);
    }

    [Fact]
    public void Parse_sets_no_link_true_when_no_link_flag_provided()
    {
        var result = AzureDevOpsFlagParser.Parse(
            new[] { "hu", "task", "Org", "Proj", "123", "-n" });

        Assert.True(result.NoLink);
    }

    [Fact]
    public void Parse_sets_no_link_true_when_long_no_link_flag_provided()
    {
        var result = AzureDevOpsFlagParser.Parse(
            new[] { "hu", "task", "Org", "Proj", "123", "--no-link" });

        Assert.True(result.NoLink);
    }

    [Fact]
    public void Parse_sets_no_link_false_when_flag_missing()
    {
        var result = AzureDevOpsFlagParser.Parse(
            new[] { "hu", "task", "Org", "Proj", "123" });

        Assert.False(result.NoLink);
    }

    [Fact]
    public void Parse_sets_trio_true_when_trio_flag_provided()
    {
        var result = AzureDevOpsFlagParser.Parse(
            new[] { "hu", "task", "Org", "Proj", "123", "--trio" });

        Assert.True(result.Trio);
    }

    [Fact]
    public void Parse_sets_trio_false_when_trio_flag_missing()
    {
        var result = AzureDevOpsFlagParser.Parse(
            new[] { "hu", "task", "Org", "Proj", "123" });

        Assert.False(result.Trio);
    }

    [Fact]
    public void Parse_sets_assigned_to_when_assigned_to_flag_provided()
    {
        var result = AzureDevOpsFlagParser.Parse(
            new[] { "task", "update", "Org", "1", "--assigned-to", "elvis.brevi@sag.gob.cl" });

        Assert.Equal("elvis.brevi@sag.gob.cl", result.AssignedTo);
    }

    [Fact]
    public void Parse_sets_assigned_to_when_assigned_to_flag_is_empty()
    {
        var result = AzureDevOpsFlagParser.Parse(
            new[] { "task", "update", "Org", "1", "--assigned-to", "" });

        Assert.Equal("", result.AssignedTo);
    }

    [Fact]
    public void Parse_sets_assigned_to_null_when_assigned_to_flag_missing()
    {
        var result = AzureDevOpsFlagParser.Parse(
            new[] { "task", "update", "Org", "1" });

        Assert.Null(result.AssignedTo);
    }
}
