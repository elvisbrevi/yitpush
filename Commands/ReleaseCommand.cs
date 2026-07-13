namespace YitPush;

public sealed record ReleasePlan(
    string RepoRoot,
    string Version,
    string CsprojPath,
    string ChangelogPath,
    string LlmsTxtPath,
    string LlmsFullTxtPath,
    string SkillMdPath,
    string InstallableSkillMdPath,
    string NupkgDir)
{
    public static ReleasePlan LoadFromRepo(string repoRoot)
    {
        var csproj = Path.Combine(repoRoot, "YitPush.csproj");
        if (!File.Exists(csproj))
        {
            throw new FileNotFoundException($"YitPush.csproj not found at {csproj}", csproj);
        }

        var version = ParseVersionFromCsproj(File.ReadAllText(csproj));

        return new ReleasePlan(
            RepoRoot: repoRoot,
            Version: version,
            CsprojPath: csproj,
            ChangelogPath: Path.Combine(repoRoot, "CHANGELOG.md"),
            LlmsTxtPath: Path.Combine(repoRoot, "llms.txt"),
            LlmsFullTxtPath: Path.Combine(repoRoot, "llms-full.txt"),
            SkillMdPath: Path.Combine(repoRoot, "SKILL.md"),
            InstallableSkillMdPath: Path.Combine(repoRoot, "skills", "yp", "SKILL.md"),
            NupkgDir: Path.Combine(repoRoot, "nupkg"));
    }

    internal static string ParseVersionFromCsproj(string csprojContent)
    {
        var match = System.Text.RegularExpressions.Regex.Match(
            csprojContent,
            @"<Version>\s*(?<v>[^<\s]+)\s*</Version>",
            System.Text.RegularExpressions.RegexOptions.Singleline);

        if (!match.Success)
        {
            throw new InvalidOperationException(
                "Could not find <Version>X.Y.Z</Version> element in csproj content.");
        }

        return match.Groups["v"].Value;
    }

    public static bool ChangelogSectionExists(string version, string changelogContent)
    {
        var heading = $"## [{version}]";
        return changelogContent.Contains(heading, StringComparison.Ordinal);
    }

    public static bool LlmsFileMentionsVersion(string version, string llmsContent)
    {
        return llmsContent.Contains(version, StringComparison.Ordinal);
    }

    public static bool SkillFileMentionsVersion(string version, string skillContent)
    {
        return skillContent.Contains(version, StringComparison.Ordinal);
    }

    public static bool SkillFilesAreIdentical(string rootSkill, string installableSkill)
    {
        return string.Equals(rootSkill, installableSkill, StringComparison.Ordinal);
    }

    public static PreFlightReport BuildPreFlightReport(
        string version,
        string csprojContent,
        string changelogContent,
        string llmsTxtContent,
        string llmsFullTxtContent,
        string rootSkillContent,
        string installableSkillContent,
        IReadOnlyList<string>? nupkgDirListing = null)
    {
        var findings = new List<ReleaseFinding>();

        var csprojVersion = ParseVersionFromCsproj(csprojContent);
        if (csprojVersion == version)
        {
            findings.Add(new ReleaseFinding(
                ReleaseFindingSeverity.Ok, "CSPROJ_VERSION",
                $"YitPush.csproj <Version>={version} matches."));
        }
        else
        {
            findings.Add(new ReleaseFinding(
                ReleaseFindingSeverity.Fail, "CSPROJ_VERSION",
                $"YitPush.csproj <Version>={csprojVersion} does not match release plan version {version}."));
        }

        if (ChangelogSectionExists(version, changelogContent))
        {
            findings.Add(new ReleaseFinding(
                ReleaseFindingSeverity.Ok, "CHANGELOG",
                $"CHANGELOG.md has ## [{version}] section."));
        }
        else
        {
            findings.Add(new ReleaseFinding(
                ReleaseFindingSeverity.Fail, "CHANGELOG",
                $"CHANGELOG.md is missing ## [{version}] section."));
        }

        AppendLlmsFinding(findings, "llms.txt", version, llmsTxtContent);
        AppendLlmsFinding(findings, "llms-full.txt", version, llmsFullTxtContent);

        if (SkillFileMentionsVersion(version, rootSkillContent))
        {
            findings.Add(new ReleaseFinding(
                ReleaseFindingSeverity.Ok, "SKILL_VERSION",
                "SKILL.md mentions the release version."));
        }
        else
        {
            findings.Add(new ReleaseFinding(
                ReleaseFindingSeverity.Fail, "SKILL_VERSION",
                "SKILL.md does not mention the release version."));
        }

        if (SkillFileMentionsVersion(version, installableSkillContent))
        {
            findings.Add(new ReleaseFinding(
                ReleaseFindingSeverity.Ok, "SKILL_VERSION",
                "skills/yp/SKILL.md mentions the release version."));
        }
        else
        {
            findings.Add(new ReleaseFinding(
                ReleaseFindingSeverity.Fail, "SKILL_VERSION",
                "skills/yp/SKILL.md does not mention the release version."));
        }

        if (SkillFilesAreIdentical(rootSkillContent, installableSkillContent))
        {
            findings.Add(new ReleaseFinding(
                ReleaseFindingSeverity.Ok, "SKILL_DRIFT",
                "SKILL.md and skills/yp/SKILL.md are byte-identical."));
        }
        else
        {
            findings.Add(new ReleaseFinding(
                ReleaseFindingSeverity.Fail, "SKILL_DRIFT",
                "SKILL.md and skills/yp/SKILL.md have drifted. Run scripts/regenerate-llms.sh or sync the installable copy."));
        }

        if (nupkgDirListing is { Count: > 0 })
        {
            var stale = nupkgDirListing
                .Where(f => f.EndsWith(".nupkg", StringComparison.OrdinalIgnoreCase)
                            && !f.Contains(version, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (stale.Count > 0)
            {
                findings.Add(new ReleaseFinding(
                    ReleaseFindingSeverity.Warn, "NUPKG_STALE",
                    $"nupkg/ has {stale.Count} artifact(s) from previous version(s): {string.Join(", ", stale)}. The script will not delete them automatically."));
            }
        }

        return new PreFlightReport(findings);
    }

    public PreFlightReport RunPreFlight()
    {
        var csprojContent = File.ReadAllText(CsprojPath);
        var changelogContent = File.ReadAllText(ChangelogPath);
        var llmsContent = File.ReadAllText(LlmsTxtPath);
        var llmsFullContent = File.ReadAllText(LlmsFullTxtPath);
        var rootSkillContent = File.ReadAllText(SkillMdPath);
        var installableSkillContent = File.ReadAllText(InstallableSkillMdPath);

        IReadOnlyList<string>? nupkgListing = null;
        if (Directory.Exists(NupkgDir))
        {
            nupkgListing = Directory.GetFiles(NupkgDir)
                .Select(Path.GetFileName)
                .Where(n => n is not null)
                .Select(n => n!)
                .ToList();
        }

        return BuildPreFlightReport(
            version: Version,
            csprojContent: csprojContent,
            changelogContent: changelogContent,
            llmsTxtContent: llmsContent,
            llmsFullTxtContent: llmsFullContent,
            rootSkillContent: rootSkillContent,
            installableSkillContent: installableSkillContent,
            nupkgDirListing: nupkgListing);
    }

    public IReadOnlyList<ReleaseStep> GetSteps()
    {
        var tag = $"v{Version}";
        var nupkgGlob = $"./nupkg/YitPush.{Version}.nupkg";

        return new List<ReleaseStep>
        {
            new(
                ReleaseStepKind.Pack,
                "Build the .nupkg artifact",
                "dotnet pack -c Release -o ./nupkg",
                "Runs `dotnet pack` against the Release configuration and writes the artifact to ./nupkg/."),

            new(
                ReleaseStepKind.NuGetPush,
                $"Push YitPush.{Version} to nuget.org",
                $"dotnet nuget push {nupkgGlob} --skip-duplicate --source https://api.nuget.org/v3/index.json",
                "Pushes the freshly-built .nupkg to the public nuget.org feed. Requires NUGET_API_KEY on first publish (subsequent pushes work with --skip-duplicate for known package+version combos)."),

            new(
                ReleaseStepKind.GitTag,
                $"Create annotated git tag {tag}",
                $"git tag -a {tag} -m \"Release {Version}\"",
                "Creates a local annotated git tag pointing at HEAD. Annotated tags carry the tagger, date, and message; they show up in `git describe` and NuGet's release history."),

            new(
                ReleaseStepKind.GitPushTag,
                $"Push tag {tag} to origin",
                $"git push origin {tag}",
                "Pushes the new tag to the remote. Once pushed, the tag is the source of truth for what shipped — every other consumer (skills.sh, downstream package mirrors) reads from there."),

            new(
                ReleaseStepKind.SkillPublish,
                "Re-publish the yp agent skill on skills.sh",
                "npx skills add elvisbrevi/yitpush --skill yp --all -y",
                "Triggers a re-index of the yp skill on skills.sh so agents pick up the v2.3.0 runtime surface. Non-fatal: skill publish failures do not block the NuGet release.",
                IsCritical: false),
        };
    }

    public string RenderBashScript()
    {
        var steps = GetSteps();
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("#!/usr/bin/env bash");
        sb.AppendLine("# Auto-generated release script for yp v" + Version);
        sb.AppendLine("# Generated by ReleasePlan.RenderBashScript() at " + DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"));
        sb.AppendLine();
        sb.AppendLine("set -euo pipefail");
        sb.AppendLine();
        sb.AppendLine("REPO_ROOT=\"$(cd \"$(dirname \"$0\")/..\" && pwd)\"");
        sb.AppendLine("cd \"$REPO_ROOT\"");
        sb.AppendLine();
        sb.AppendLine("echo \"Releasing yp v" + Version + " from $REPO_ROOT\"");
        sb.AppendLine();

        for (var i = 0; i < steps.Count; i++)
        {
            var step = steps[i];
            var ordinal = $"[{i + 1}/{steps.Count}]";
            sb.AppendLine($"# {ordinal} {step.Label}");
            sb.AppendLine($"# {step.Description}");
            sb.AppendLine($"echo \"{ordinal} {step.Label} ...\"");

            if (step.IsCritical)
            {
                sb.AppendLine(step.RenderForShell());
            }
            else
            {
                sb.AppendLine("# Non-fatal: this step's failure should not block the rest of the release.");
                sb.AppendLine(step.RenderForShell() + " || { echo \"WARN: " + step.Kind + " failed (non-fatal). Continuing with the rest of the release.\" >&2; }");
            }
            sb.AppendLine();
        }

        return sb.ToString();
    }

    private static void AppendLlmsFinding(List<ReleaseFinding> findings, string fileName, string version, string content)
    {
        if (LlmsFileMentionsVersion(version, content))
        {
            findings.Add(new ReleaseFinding(
                ReleaseFindingSeverity.Ok, "LLMS",
                $"{fileName} mentions the release version."));
        }
        else
        {
            findings.Add(new ReleaseFinding(
                ReleaseFindingSeverity.Fail, "LLMS",
                $"{fileName} does not mention the release version {version}. Run scripts/regenerate-llms.sh."));
        }
    }
}

public enum ReleaseFindingSeverity
{
    Ok,
    Warn,
    Fail,
}

public sealed record ReleaseFinding(ReleaseFindingSeverity Severity, string Category, string Message);

public sealed record PreFlightReport(IReadOnlyList<ReleaseFinding> Findings)
{
    public bool IsReadyToShip => !Findings.Any(f => f.Severity == ReleaseFindingSeverity.Fail);

    public int FailureCount => Findings.Count(f => f.Severity == ReleaseFindingSeverity.Fail);

    public int WarningCount => Findings.Count(f => f.Severity == ReleaseFindingSeverity.Warn);
}

public enum ReleaseStepKind
{
    Pack,
    NuGetPush,
    GitTag,
    GitPushTag,
    SkillPublish,
}

public sealed record ReleaseStep(
    ReleaseStepKind Kind,
    string Label,
    string ShellCommand,
    string Description,
    bool IsCritical = true)
{
    public string RenderForShell() => ShellCommand;
}
