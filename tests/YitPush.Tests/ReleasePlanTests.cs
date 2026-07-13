using YitPush;

namespace YitPush.Tests;

public class ReleasePlanTests
{
    private static readonly string RepoRoot = SkillFileAlignmentTests.LocateRepoRootPublic;

    [Fact]
    public void LoadFromRepo_reads_version_from_csproj()
    {
        var plan = ReleasePlan.LoadFromRepo(RepoRoot);

        Assert.Equal("2.3.0", plan.Version);
    }

    [Fact]
    public void LoadFromRepo_returns_paths_relative_to_repo_root()
    {
        var plan = ReleasePlan.LoadFromRepo(RepoRoot);

        Assert.Equal(Path.Combine(RepoRoot, "YitPush.csproj"), plan.CsprojPath);
        Assert.Equal(Path.Combine(RepoRoot, "CHANGELOG.md"), plan.ChangelogPath);
        Assert.Equal(Path.Combine(RepoRoot, "llms.txt"), plan.LlmsTxtPath);
        Assert.Equal(Path.Combine(RepoRoot, "llms-full.txt"), plan.LlmsFullTxtPath);
        Assert.Equal(Path.Combine(RepoRoot, "SKILL.md"), plan.SkillMdPath);
        Assert.Equal(Path.Combine(RepoRoot, "skills", "yp", "SKILL.md"), plan.InstallableSkillMdPath);
        Assert.Equal(Path.Combine(RepoRoot, "nupkg"), plan.NupkgDir);
    }

    [Fact]
    public void ParseVersionFromCsproj_extracts_simple_version()
    {
        var content = """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <Version>1.2.3</Version>
              </PropertyGroup>
            </Project>
            """;

        Assert.Equal("1.2.3", ReleasePlan.ParseVersionFromCsproj(content));
    }

    [Fact]
    public void ParseVersionFromCsproj_tolerates_whitespace_around_value()
    {
        var content = """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <Version>  4.5.6-pre.1  </Version>
              </PropertyGroup>
            </Project>
            """;

        Assert.Equal("4.5.6-pre.1", ReleasePlan.ParseVersionFromCsproj(content));
    }

    [Fact]
    public void ParseVersionFromCsproj_throws_when_missing()
    {
        var content = """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
              </PropertyGroup>
            </Project>
            """;

        Assert.Throws<InvalidOperationException>(() => ReleasePlan.ParseVersionFromCsproj(content));
    }

    [Fact]
    public void ChangelogSectionExists_returns_true_when_heading_present()
    {
        var changelog = """
            # Changelog

            ## [Unreleased]

            ## [2.3.0] — 2026-07-10 — the big one

            ### Highlights
            - something

            ## [2.2.2] — 2026-06-22
            """;

        Assert.True(ReleasePlan.ChangelogSectionExists("2.3.0", changelog));
    }

    [Fact]
    public void ChangelogSectionExists_returns_false_when_heading_missing()
    {
        var changelog = """
            # Changelog

            ## [Unreleased]

            ## [2.2.2] — 2026-06-22
            """;

        Assert.False(ReleasePlan.ChangelogSectionExists("2.3.0", changelog));
    }

    [Fact]
    public void LlmsFileMentionsVersion_returns_true_when_version_present()
    {
        var content = $"some preamble\n\nVersion: 2.3.0\nmore content\n";

        Assert.True(ReleasePlan.LlmsFileMentionsVersion("2.3.0", content));
    }

    [Fact]
    public void LlmsFileMentionsVersion_returns_false_when_version_missing()
    {
        var content = "Version: 2.2.2 — only this version is mentioned\n";

        Assert.False(ReleasePlan.LlmsFileMentionsVersion("2.3.0", content));
    }

    [Fact]
    public void SkillFileMentionsVersion_returns_true_when_version_present()
    {
        var content = "# yp skill\n\nCurrent version: 2.3.0\n";

        Assert.True(ReleasePlan.SkillFileMentionsVersion("2.3.0", content));
    }

    [Fact]
    public void SkillFileMentionsVersion_returns_false_when_version_missing()
    {
        var content = "# yp skill\n\nCurrent version: 2.2.2\n";

        Assert.False(ReleasePlan.SkillFileMentionsVersion("2.3.0", content));
    }

    [Fact]
    public void SkillFilesAreIdentical_returns_true_when_same_content()
    {
        var a = "same content\n";
        var b = "same content\n";

        Assert.True(ReleasePlan.SkillFilesAreIdentical(a, b));
    }

    [Fact]
    public void SkillFilesAreIdentical_returns_false_when_drifted()
    {
        var a = "content from SKILL.md\n";
        var b = "content from skills/yp/SKILL.md\n";

        Assert.False(ReleasePlan.SkillFilesAreIdentical(a, b));
    }

    [Fact]
    public void PreFlightReport_marks_every_check_OK_when_release_is_ready()
    {
        var version = "9.9.9-test";
        var csproj = $"<Project><PropertyGroup><Version>{version}</Version></PropertyGroup></Project>";
        var changelog = $"""
            # Changelog

            ## [{version}] — 2099-01-01

            some content
            """;
        var llms = $"version stamp: {version}\n";
        var skill = $"version stamp: {version}\n";

        var report = ReleasePlan.BuildPreFlightReport(version, csproj, changelog, llms, llms, skill, skill);

        Assert.True(report.IsReadyToShip);
        Assert.DoesNotContain(report.Findings, f => f.Severity == ReleaseFindingSeverity.Fail);
    }

    [Fact]
    public void PreFlightReport_marks_changelog_missing_as_FAIL()
    {
        var version = "9.9.9-test";
        var csproj = $"<Project><PropertyGroup><Version>{version}</Version></PropertyGroup></Project>";
        var changelog = "# Changelog\n\n## [2.2.2]\n"; // version NOT present
        var llms = $"version stamp: {version}\n";
        var skill = $"version stamp: {version}\n";

        var report = ReleasePlan.BuildPreFlightReport(version, csproj, changelog, llms, llms, skill, skill);

        Assert.False(report.IsReadyToShip);
        Assert.Contains(report.Findings,
            f => f.Severity == ReleaseFindingSeverity.Fail && f.Category == "CHANGELOG");
    }

    [Fact]
    public void PreFlightReport_marks_skill_files_drift_as_FAIL()
    {
        var version = "9.9.9-test";
        var csproj = $"<Project><PropertyGroup><Version>{version}</Version></PropertyGroup></Project>";
        var changelog = $"""
            # Changelog

            ## [{version}]
            """;
        var llms = $"version stamp: {version}\n";
        var skillRoot = $"# yp\n\nversion: {version}\n";
        var skillInstallable = "# yp\n\nversion: 2.2.2 (drifted)\n";

        var report = ReleasePlan.BuildPreFlightReport(version, csproj, changelog, llms, llms, skillRoot, skillInstallable);

        Assert.False(report.IsReadyToShip);
        Assert.Contains(report.Findings,
            f => f.Severity == ReleaseFindingSeverity.Fail && f.Category == "SKILL_DRIFT");
    }

    [Fact]
    public void PreFlightReport_marks_missing_llms_version_as_FAIL()
    {
        var version = "9.9.9-test";
        var csproj = $"<Project><PropertyGroup><Version>{version}</Version></PropertyGroup></Project>";
        var changelog = $"""
            # Changelog

            ## [{version}]
            """;
        var llmsMissing = "no version here\n";
        var skill = $"version: {version}\n";

        var report = ReleasePlan.BuildPreFlightReport(version, csproj, changelog, llmsMissing, llmsMissing, skill, skill);

        Assert.False(report.IsReadyToShip);
        Assert.Equal(2, report.Findings.Count(f => f.Severity == ReleaseFindingSeverity.Fail && f.Category == "LLMS"));
    }

    [Fact]
    public void PreFlightReport_marks_skill_missing_version_as_FAIL()
    {
        var version = "9.9.9-test";
        var csproj = $"<Project><PropertyGroup><Version>{version}</Version></PropertyGroup></Project>";
        var changelog = $"""
            # Changelog

            ## [{version}]
            """;
        var llms = $"version: {version}\n";
        var skillRoot = "# yp\nno version here\n";
        var skillInstallable = "# yp\nno version here\n";

        var report = ReleasePlan.BuildPreFlightReport(version, csproj, changelog, llms, llms, skillRoot, skillInstallable);

        Assert.False(report.IsReadyToShip);
        Assert.Equal(2, report.Findings.Count(f => f.Severity == ReleaseFindingSeverity.Fail && f.Category == "SKILL_VERSION"));
    }

    [Fact]
    public void PreFlightReport_warn_when_nupkg_dir_has_stale_artifacts()
    {
        var version = "9.9.9-test";
        var csproj = $"<Project><PropertyGroup><Version>{version}</Version></PropertyGroup></Project>";
        var changelog = $"""
            # Changelog

            ## [{version}]
            """;
        var llms = $"version: {version}\n";
        var skill = $"version: {version}\n";

        var report = ReleasePlan.BuildPreFlightReport(
            version: version,
            csprojContent: csproj,
            changelogContent: changelog,
            llmsTxtContent: llms,
            llmsFullTxtContent: llms,
            rootSkillContent: skill,
            installableSkillContent: skill,
            nupkgDirListing: new[] { "YitPush.9.9.8.nupkg", "YitPush.9.9.8.symbols.nupkg" });

        Assert.True(report.IsReadyToShip);
        Assert.Contains(report.Findings,
            f => f.Severity == ReleaseFindingSeverity.Warn && f.Category == "NUPKG_STALE");
    }

    [Fact]
    public void RunPreFlight_against_real_repo_for_v2_3_0_is_ready_to_ship()
    {
        var plan = ReleasePlan.LoadFromRepo(RepoRoot);
        var report = plan.RunPreFlight();

        Assert.True(report.IsReadyToShip,
            "Real-repo pre-flight should be ready to ship. Failures: " +
            string.Join(" | ", report.Findings
                .Where(f => f.Severity == ReleaseFindingSeverity.Fail)
                .Select(f => $"[{f.Category}] {f.Message}")));
    }

    [Fact]
    public void BuildPreFlightReport_stale_nupkg_listing_yields_WARN_not_FAIL()
    {
        var version = "9.9.9-test";
        var csproj = $"<Project><PropertyGroup><Version>{version}</Version></PropertyGroup></Project>";
        var changelog = $"# Changelog\n\n## [{version}]\n";
        var llms = $"version: {version}\n";
        var skill = $"version: {version}\n";

        var report = ReleasePlan.BuildPreFlightReport(
            version: version,
            csprojContent: csproj,
            changelogContent: changelog,
            llmsTxtContent: llms,
            llmsFullTxtContent: llms,
            rootSkillContent: skill,
            installableSkillContent: skill,
            nupkgDirListing: new[] { "YitPush.2.1.1.nupkg", "YitPush.2.1.2.nupkg" });

        Assert.True(report.IsReadyToShip,
            "Stale nupkg/ files are a WARN, not a FAIL — release is still ready to ship.");
        Assert.Contains(report.Findings,
            f => f.Severity == ReleaseFindingSeverity.Warn && f.Category == "NUPKG_STALE");
        Assert.DoesNotContain(report.Findings,
            f => f.Severity == ReleaseFindingSeverity.Fail && f.Category == "NUPKG_STALE");
    }

    [Fact]
    public void GetSteps_returns_pack_tag_push_and_skill_in_order()
    {
        var plan = ReleasePlan.LoadFromRepo(RepoRoot);
        var steps = plan.GetSteps();

        var kinds = steps.Select(s => s.Kind).ToList();
        Assert.Equal(new[]
        {
            ReleaseStepKind.Pack,
            ReleaseStepKind.NuGetPush,
            ReleaseStepKind.GitTag,
            ReleaseStepKind.GitPushTag,
            ReleaseStepKind.SkillPublish,
        }, kinds);
    }

    [Fact]
    public void GetSteps_substitutes_version_into_tag_command()
    {
        var plan = ReleasePlan.LoadFromRepo(RepoRoot);
        var tagStep = plan.GetSteps().Single(s => s.Kind == ReleaseStepKind.GitTag);

        Assert.Contains("v2.3.0", tagStep.ShellCommand);
    }

    [Fact]
    public void GetSteps_substitutes_version_into_nupkg_filename_hint()
    {
        var plan = ReleasePlan.LoadFromRepo(RepoRoot);
        var nupkgStep = plan.GetSteps().Single(s => s.Kind == ReleaseStepKind.NuGetPush);

        Assert.Contains("YitPush.2.3.0", nupkgStep.ShellCommand);
    }

    [Fact]
    public void GetSteps_skill_step_invokes_skills_add()
    {
        var plan = ReleasePlan.LoadFromRepo(RepoRoot);
        var skillStep = plan.GetSteps().Single(s => s.Kind == ReleaseStepKind.SkillPublish);

        Assert.Contains("npx skills add elvisbrevi/yitpush", skillStep.ShellCommand);
    }

    [Fact]
    public void GetSteps_pack_step_targets_release_configuration()
    {
        var plan = ReleasePlan.LoadFromRepo(RepoRoot);
        var packStep = plan.GetSteps().Single(s => s.Kind == ReleaseStepKind.Pack);

        Assert.Contains("-c Release", packStep.ShellCommand);
    }

    [Fact]
    public void ReleaseStep_RenderForShell_returns_the_shell_command()
    {
        var step = new ReleaseStep(
            ReleaseStepKind.Pack, "label", "echo hello", "description");

        Assert.Equal("echo hello", step.RenderForShell());
    }

    [Fact]
    public void ReleasePlan_RenderBashScript_includes_set_e_and_one_step_per_line()
    {
        var plan = ReleasePlan.LoadFromRepo(RepoRoot);
        var script = plan.RenderBashScript();

        Assert.Contains("set -euo pipefail", script);
        Assert.Contains("dotnet pack -c Release -o ./nupkg", script);
        Assert.Contains("dotnet nuget push ./nupkg/YitPush.2.3.0.nupkg", script);
        Assert.Contains("git tag -a v2.3.0", script);
        Assert.Contains("git push origin v2.3.0", script);
        Assert.Contains("npx skills add elvisbrevi/yitpush", script);
    }

    [Fact]
    public void ReleasePlan_RenderBashScript_includes_progress_echoes_for_each_step()
    {
        var plan = ReleasePlan.LoadFromRepo(RepoRoot);
        var script = plan.RenderBashScript();

        Assert.Contains("[1/5]", script);
        Assert.Contains("[2/5]", script);
        Assert.Contains("[3/5]", script);
        Assert.Contains("[4/5]", script);
        Assert.Contains("[5/5]", script);
    }

    [Fact]
    public void SkillPublish_step_is_marked_non_critical()
    {
        var plan = ReleasePlan.LoadFromRepo(RepoRoot);
        var skillStep = plan.GetSteps().Single(s => s.Kind == ReleaseStepKind.SkillPublish);

        Assert.False(skillStep.IsCritical,
            "skills.sh re-publish must be non-fatal so a transient network blip doesn't fail the NuGet release.");
    }

    [Fact]
    public void Pack_tag_and_push_steps_are_critical()
    {
        var plan = ReleasePlan.LoadFromRepo(RepoRoot);
        var critical = plan.GetSteps()
            .Where(s => s.IsCritical)
            .Select(s => s.Kind)
            .ToHashSet();

        Assert.Contains(ReleaseStepKind.Pack, critical);
        Assert.Contains(ReleaseStepKind.NuGetPush, critical);
        Assert.Contains(ReleaseStepKind.GitTag, critical);
        Assert.Contains(ReleaseStepKind.GitPushTag, critical);
    }

    [Fact]
    public void ReleasePlan_RenderBashScript_wraps_non_critical_step_in_rescue_block()
    {
        var plan = ReleasePlan.LoadFromRepo(RepoRoot);
        var script = plan.RenderBashScript();

        Assert.Contains("|| {", script);
        Assert.Contains("non-fatal", script);
        Assert.Contains("Continuing with the rest of the release", script);
    }

    [Fact]
    public void ReleasePlan_RenderBashScript_does_not_wrap_critical_steps()
    {
        var plan = ReleasePlan.LoadFromRepo(RepoRoot);
        var script = plan.RenderBashScript();

        var criticalLines = script.Split('\n')
            .Where(l => l.Contains("dotnet pack") || l.Contains("dotnet nuget push") || l.Contains("git tag") || l.Contains("git push origin"))
            .ToList();

        Assert.NotEmpty(criticalLines);
        Assert.DoesNotContain(criticalLines, l => l.Contains("|| {"));
    }
}
