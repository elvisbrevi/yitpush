using YitPush;

namespace YitPush.Tests;

public class SkillFileAlignmentTests
{
    public static readonly string LocateRepoRootPublic = LocateRepoRoot();

    private static string RepoRoot => LocateRepoRootPublic;

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
    public void RootSkillMd_mentions_subcommand(string needle)
    {
        var content = File.ReadAllText(Path.Combine(RepoRoot, "SKILL.md"));
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
    public void InstallableSkillMd_mentions_subcommand(string needle)
    {
        var content = File.ReadAllText(Path.Combine(RepoRoot, "skills/yp/SKILL.md"));
        Assert.Contains(needle, content);
    }

    [Fact]
    public void RootSkillMd_and_installable_skillMd_have_matching_subcommand_sections()
    {
        var root = File.ReadAllText(Path.Combine(RepoRoot, "SKILL.md"));
        var installable = File.ReadAllText(Path.Combine(RepoRoot, "skills/yp/SKILL.md"));

        var expected = Program.HelpAzureDevOpsSubcommands
            .Concat(Program.HelpPrSubcommands.Select(s => "pr " + s))
            .Concat(Program.HelpTopLevelSubcommands);

        foreach (var needle in expected)
        {
            Assert.Contains(needle, root);
            Assert.Contains(needle, installable);
        }
    }

    [Fact]
    public void RootSkillMd_and_installable_skillMd_are_byte_identical()
    {
        var root = File.ReadAllText(LocateRepoRootPublic + "/SKILL.md");
        var installable = File.ReadAllText(LocateRepoRootPublic + "/skills/yp/SKILL.md");
        Assert.Equal(root, installable);
    }

    private static string LocateRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "YitPush.csproj")))
        {
            dir = dir.Parent;
        }
        if (dir == null) throw new InvalidOperationException("Could not locate YitPush.csproj walking up from " + AppContext.BaseDirectory);
        return dir.FullName;
    }
}
