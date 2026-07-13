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

    [Theory]
    [InlineData("SKILL.md")]
    [InlineData("skills/yp/SKILL.md")]
    public void SkillMd_starts_with_valid_yaml_frontmatter(string relativePath)
    {
        var content = File.ReadAllText(Path.Combine(RepoRoot, relativePath));

        Assert.StartsWith("---\n", content);

        var closingDash = content.IndexOf("\n---\n", StringComparison.Ordinal);
        Assert.True(closingDash > 0, $"Expected a closing '---' fence after the frontmatter in {relativePath}.");

        var frontmatter = content.Substring(4, closingDash - 4);
        Assert.Contains("name: yp", frontmatter);

        var descriptionIdx = frontmatter.IndexOf("description:", StringComparison.Ordinal);
        Assert.True(descriptionIdx >= 0, $"Expected 'description:' field in frontmatter of {relativePath}.");
        var descriptionLine = frontmatter.Substring(descriptionIdx);
        var descriptionValue = descriptionLine.Substring("description:".Length).Trim();
        Assert.False(string.IsNullOrWhiteSpace(descriptionValue),
            $"description must be non-empty in {relativePath}.");
        Assert.True(descriptionValue.Length <= 1024,
            $"description must be <= 1024 chars per Agent Skills spec (got {descriptionValue.Length}).");
    }

    [Theory]
    [InlineData("SKILL.md")]
    [InlineData("skills/yp/SKILL.md")]
    public void SkillMd_frontmatter_description_mentions_key_capabilities(string relativePath)
    {
        var content = File.ReadAllText(Path.Combine(RepoRoot, relativePath));
        var closingDash = content.IndexOf("\n---\n", StringComparison.Ordinal);
        var frontmatter = content.Substring(4, closingDash - 4);

        var descriptionIdx = frontmatter.IndexOf("description:", StringComparison.Ordinal);
        var descriptionLine = frontmatter.Substring(descriptionIdx);
        var descriptionValue = descriptionLine.Substring("description:".Length).Trim();

        var requiredKeywords = new[] { "commit", "PR", "Azure DevOps" };
        var missing = requiredKeywords.Where(k => !descriptionValue.Contains(k, StringComparison.Ordinal)).ToList();

        Assert.True(missing.Count == 0,
            $"{relativePath} description is missing required keywords: {string.Join(", ", missing)}. " +
            $"Got: \"{descriptionValue}\"");
    }

    [Fact]
    public void InstallableSkillMd_frontmatter_name_matches_parent_directory()
    {
        const string relativePath = "skills/yp/SKILL.md";
        var content = File.ReadAllText(Path.Combine(RepoRoot, relativePath));
        var closingDash = content.IndexOf("\n---\n", StringComparison.Ordinal);
        var frontmatter = content.Substring(4, closingDash - 4);

        var nameIdx = frontmatter.IndexOf("name:", StringComparison.Ordinal);
        Assert.True(nameIdx >= 0, $"Expected 'name:' field in frontmatter of {relativePath}.");
        var nameLine = frontmatter.Substring(nameIdx);
        var nameValue = nameLine.Substring("name:".Length).Trim().Split('\n')[0].Trim();

        Assert.Equal("yp", nameValue);
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
