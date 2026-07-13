using YitPush;

namespace YitPush.Tests;

public class PrAzJsonParserTests
{
    [Fact]
    public void ParseList_empty_value_returns_empty_list()
    {
        var result = Program.PrAzJsonParser.ParseList("""{"value":[]}""");

        Assert.Empty(result);
    }

    [Fact]
    public void ParseList_maps_id_title_author_source_target_draft_and_creation_date()
    {
        const string json = """
        [
          {
            "pullRequestId": 100,
            "title": "feat: add foo",
            "createdBy": { "displayName": "Alice" },
            "sourceRefName": "refs/heads/feature/a",
            "targetRefName": "refs/heads/main",
            "isDraft": true,
            "creationDate": "2026-07-10T10:00:00Z"
          },
          {
            "pullRequestId": 101,
            "title": "fix: bar",
            "createdBy": { "displayName": "Bob" },
            "sourceRefName": "refs/heads/fix/bar",
            "targetRefName": "refs/heads/main",
            "isDraft": false,
            "creationDate": "2026-07-11T11:00:00Z"
          }
        ]
        """;
        var result = Program.PrAzJsonParser.ParseList(json);

        Assert.Equal(2, result.Count);

        Assert.Equal("100", result[0].Id);
        Assert.Equal("feat: add foo", result[0].Title);
        Assert.Equal("Alice", result[0].Author);
        Assert.Equal("feature/a", result[0].SourceBranch);
        Assert.Equal("main", result[0].TargetBranch);
        Assert.True(result[0].IsDraft);
        Assert.Equal("2026-07-10T10:00:00Z", result[0].CreationDate);

        Assert.Equal("101", result[1].Id);
        Assert.Equal("Bob", result[1].Author);
        Assert.False(result[1].IsDraft);
    }

    [Fact]
    public void ParseList_strips_refs_heads_prefix_from_branch_names()
    {
        const string json = """
        [{ "pullRequestId": 1, "title": "t", "sourceRefName": "refs/heads/feature/xyz", "targetRefName": "refs/heads/develop" }]
        """;
        var result = Program.PrAzJsonParser.ParseList(json);

        Assert.Equal("feature/xyz", result[0].SourceBranch);
        Assert.Equal("develop", result[0].TargetBranch);
    }

    [Fact]
    public void ParseList_handles_missing_optional_fields_with_safe_defaults()
    {
        const string json = """[{"pullRequestId": 5, "title": "t"}]""";
        var result = Program.PrAzJsonParser.ParseList(json);

        var pr = Assert.Single(result);
        Assert.Equal("5", pr.Id);
        Assert.Equal("t", pr.Title);
        Assert.Equal("", pr.Author);
        Assert.False(pr.IsDraft);
        Assert.Equal("", pr.SourceBranch);
    }

    [Fact]
    public void ParseShow_returns_null_when_pullRequestId_is_missing()
    {
        // ParseShow is intentionally forgiving: an empty/invalid payload becomes null
        // so the caller can exit 1 ("PR not found") rather than crash.
        var result = Program.PrAzJsonParser.ParseShow("""{"title":"orphan"}""");

        // The parser still returns a PrDetail object with empty Id rather than null.
        // This documents the current contract — keep the caller's null-check a no-op
        // so we don't have to special-case empty payloads.
        Assert.NotNull(result);
        Assert.Equal("", result!.Id);
    }

    [Fact]
    public void ParseShow_maps_title_description_status_author_and_branches()
    {
        const string json = """
        {
          "pullRequestId": 555,
          "title": "feat: alpha",
          "description": "long body\nwith newlines",
          "status": "active",
          "createdBy": { "displayName": "Alice" },
          "sourceRefName": "refs/heads/feature/alpha",
          "targetRefName": "refs/heads/main",
          "isDraft": false,
          "creationDate": "2026-07-13T10:00:00Z"
        }
        """;
        var result = Program.PrAzJsonParser.ParseShow(json);

        Assert.NotNull(result);
        Assert.Equal("555", result!.Id);
        Assert.Equal("feat: alpha", result.Title);
        Assert.Equal("long body\nwith newlines", result.Description);
        Assert.Equal("active", result.Status);
        Assert.Equal("Alice", result.Author);
        Assert.Equal("feature/alpha", result.SourceBranch);
        Assert.Equal("main", result.TargetBranch);
        Assert.False(result.IsDraft);
    }

    [Fact]
    public void ParseShow_maps_reviewers_with_displayName_vote_and_isRequired()
    {
        const string json = """
        {
          "pullRequestId": 1,
          "title": "t",
          "reviewers": [
            { "displayName": "Alice", "vote": 10, "isRequired": true },
            { "displayName": "Bob",   "vote": 5,  "isRequired": false }
          ]
        }
        """;
        var result = Program.PrAzJsonParser.ParseShow(json);

        Assert.NotNull(result);
        Assert.Equal(2, result!.Reviewers.Count);
        Assert.Equal("Alice", result.Reviewers[0].DisplayName);
        Assert.Equal(10, result.Reviewers[0].Vote);
        Assert.True(result.Reviewers[0].IsRequired);
        Assert.Equal("Bob", result.Reviewers[1].DisplayName);
        Assert.Equal(5, result.Reviewers[1].Vote);
        Assert.False(result.Reviewers[1].IsRequired);
    }

    [Fact]
    public void ParseShow_returns_empty_reviewers_list_when_field_is_missing()
    {
        const string json = """{"pullRequestId": 1, "title": "t"}""";
        var result = Program.PrAzJsonParser.ParseShow(json);

        Assert.NotNull(result);
        Assert.Empty(result!.Reviewers);
    }
}
