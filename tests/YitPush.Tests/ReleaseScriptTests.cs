using YitPush;

namespace YitPush.Tests;

public class ReleaseScriptTests
{
    private static readonly string RepoRoot = SkillFileAlignmentTests.LocateRepoRootPublic;

    [Fact]
    public void ReleaseScript_exists_and_is_executable()
    {
        var path = Path.Combine(RepoRoot, "scripts", "release-2.3.0.sh");
        Assert.True(File.Exists(path), $"Expected {path} to exist.");

        var perms = File.GetUnixFileMode(path);
        Assert.True((perms & UnixFileMode.UserExecute) != 0,
            $"Expected {path} to be executable (got mode {perms}).");
    }

    [Fact]
    public void ReleaseScript_dry_run_exits_zero_against_real_repo()
    {
        var path = Path.Combine(RepoRoot, "scripts", "release-2.3.0.sh");
        if (!File.Exists(path))
        {
            return; // guarded by the other test
        }

        var psi = new System.Diagnostics.ProcessStartInfo
        {
            FileName = "/bin/bash",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        psi.ArgumentList.Add(path);
        psi.ArgumentList.Add("--dry-run");

        using var proc = System.Diagnostics.Process.Start(psi)!;
        var stdout = proc.StandardOutput.ReadToEnd();
        var stderr = proc.StandardError.ReadToEnd();
        proc.WaitForExit(30_000);

        Assert.True(proc.ExitCode == 0,
            $"Expected --dry-run to exit 0, got {proc.ExitCode}.\n--- stdout ---\n{stdout}\n--- stderr ---\n{stderr}");
    }

    [Fact]
    public void ReleaseScript_push_command_uses_version_specific_path_not_directory_glob()
    {
        var path = Path.Combine(RepoRoot, "scripts", "release-2.3.0.sh");
        if (!File.Exists(path))
        {
            return;
        }

        var content = File.ReadAllText(path);

        Assert.Contains("\"./nupkg/YitPush.${VERSION}.nupkg\"", content);

        Assert.DoesNotContain("./nupkg/*.nupkg", content);
        Assert.DoesNotContain("./nupkg/${NUPKG_FILE}", content);
    }

    [Fact]
    public void ReleaseScript_cleanup_uses_aggressive_nupkg_pattern()
    {
        var path = Path.Combine(RepoRoot, "scripts", "release-2.3.0.sh");
        if (!File.Exists(path))
        {
            return;
        }

        var content = File.ReadAllText(path);

        Assert.Contains("nupkg/*.nupkg", content);
        Assert.Contains("nupkg/*.symbols.nupkg", content);
        Assert.Contains("rm -f", content);
    }

    [Fact]
    public void ReleaseScript_checks_NUGET_API_KEY_in_preflight()
    {
        var path = Path.Combine(RepoRoot, "scripts", "release-2.3.0.sh");
        if (!File.Exists(path))
        {
            return;
        }

        var content = File.ReadAllText(path);

        Assert.Contains("NUGET_API_KEY", content);
        Assert.Contains("[NUGET_API_KEY]", content);
    }

    [Fact]
    public void ReleaseScript_git_tag_step_is_idempotent()
    {
        var path = Path.Combine(RepoRoot, "scripts", "release-2.3.0.sh");
        if (!File.Exists(path))
        {
            return;
        }

        var content = File.ReadAllText(path);

        Assert.Contains("git rev-parse", content);
        Assert.Contains("already exists", content);
    }

    [Fact]
    public void ReleaseScript_version_can_be_overridden_via_env_var()
    {
        var path = Path.Combine(RepoRoot, "scripts", "release-2.3.0.sh");
        if (!File.Exists(path))
        {
            return;
        }

        var content = File.ReadAllText(path);

        Assert.Contains("VERSION=\"${VERSION:-", content);
    }
}
