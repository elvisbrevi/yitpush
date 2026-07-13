using System.Text.Json;
using YitPush;

namespace YitPush.Tests;

public class PrJsonContractTests
{
    [Fact]
    public void PrSummary_serializes_with_stable_camelCase_keys_for_jq()
    {
        var summary = new PrSummary
        {
            Id = "100",
            Title = "feat: x",
            Author = "Alice",
            SourceBranch = "feature/x",
            TargetBranch = "main",
            IsDraft = true,
            CreationDate = "2026-07-10T10:00:00Z"
        };

        var json = JsonSerializer.Serialize(summary);

        Assert.Contains("\"id\":\"100\"", json);
        Assert.Contains("\"title\":\"feat: x\"", json);
        Assert.Contains("\"author\":\"Alice\"", json);
        Assert.Contains("\"sourceBranch\":\"feature/x\"", json);
        Assert.Contains("\"targetBranch\":\"main\"", json);
        Assert.Contains("\"isDraft\":true", json);
    }

    [Fact]
    public void PrDetail_serializes_reviewers_and_changedFiles_nested_arrays()
    {
        var detail = new PrDetail
        {
            Id = "1",
            Title = "t",
            Reviewers = new List<PrReviewer>
            {
                new() { DisplayName = "Alice", Vote = 10, IsRequired = true }
            },
            ChangedFiles = new List<PrChangedFile>
            {
                new() { Path = "src/Foo.cs" }
            }
        };

        var json = JsonSerializer.Serialize(detail);

        Assert.Contains("\"reviewers\":[", json);
        Assert.Contains("\"displayName\":\"Alice\"", json);
        Assert.Contains("\"vote\":10", json);
        Assert.Contains("\"isRequired\":true", json);
        Assert.Contains("\"changedFiles\":[", json);
        Assert.Contains("\"path\":\"src/Foo.cs\"", json);
    }

    [Fact]
    public void PrThread_serializes_with_comments_and_threadContext_fields()
    {
        var thread = new AzDevOpsPrClient.PrThread(
            Id: 101,
            Status: "active",
            FilePath: "/src/Foo.cs",
            LineNumber: 42,
            Comments: new List<AzDevOpsPrClient.PrThreadComment>
            {
                new(Id: 1, Author: "Alice", Date: "2026-07-13T10:00:00Z", Content: "Fix this")
            });

        var json = JsonSerializer.Serialize(thread);

        Assert.Contains("\"id\":101", json);
        Assert.Contains("\"status\":\"active\"", json);
        Assert.Contains("\"filePath\":\"/src/Foo.cs\"", json);
        Assert.Contains("\"lineNumber\":42", json);
        Assert.Contains("\"comments\":[", json);
        Assert.Contains("\"author\":\"Alice\"", json);
        Assert.Contains("\"content\":\"Fix this\"", json);
    }

    [Fact]
    public void PrThread_serializes_null_filePath_and_lineNumber_as_nulls()
    {
        var thread = new AzDevOpsPrClient.PrThread(
            Id: 1, Status: "fixed", FilePath: null, LineNumber: null,
            Comments: new List<AzDevOpsPrClient.PrThreadComment>());

        var json = JsonSerializer.Serialize(thread);

        Assert.Contains("\"filePath\":null", json);
        Assert.Contains("\"lineNumber\":null", json);
        Assert.Contains("\"comments\":[]", json);
    }
}
