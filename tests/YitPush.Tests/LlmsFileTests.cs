using YitPush;

namespace YitPush.Tests;

public class LlmsFileTests
{
    private static readonly string RepoRoot = SkillFileAlignmentTests.LocateRepoRootPublic;

    [Theory]
    [InlineData("task delete")]
    [InlineData("task attach")]
    [InlineData("resolve-field")]
    [InlineData("refresh-fields")]
    [InlineData("diff")]
    [InlineData("pr list")]
    [InlineData("pr show")]
    [InlineData("pr comments")]
    [InlineData("pr reply")]
    [InlineData("pr create")]
    public void LlmsTxt_mentions_subcommand(string needle)
    {
        var content = File.ReadAllText(Path.Combine(RepoRoot, "llms.txt"));
        Assert.Contains(needle, content);
    }

    [Theory]
    [InlineData("task delete")]
    [InlineData("task attach")]
    [InlineData("resolve-field")]
    [InlineData("refresh-fields")]
    [InlineData("diff")]
    [InlineData("pr list")]
    [InlineData("pr show")]
    [InlineData("pr comments")]
    [InlineData("pr reply")]
    [InlineData("pr create")]
    public void LlmsFullTxt_mentions_subcommand(string needle)
    {
        var content = File.ReadAllText(Path.Combine(RepoRoot, "llms-full.txt"));
        Assert.Contains(needle, content);
    }

    [Fact]
    public void LlmsTxt_reflects_v2_3_0_version()
    {
        var content = File.ReadAllText(Path.Combine(RepoRoot, "llms.txt"));
        Assert.Contains("2.3.0", content);
    }

    [Fact]
    public void LlmsFullTxt_reflects_v2_3_0_version()
    {
        var content = File.ReadAllText(Path.Combine(RepoRoot, "llms-full.txt"));
        Assert.Contains("2.3.0", content);
    }

    [Fact]
    public void LlmsFullTxt_mentions_nvidia_nim_provider()
    {
        var content = File.ReadAllText(Path.Combine(RepoRoot, "llms-full.txt"));
        Assert.Contains("NVIDIA NIM", content);
    }
}
