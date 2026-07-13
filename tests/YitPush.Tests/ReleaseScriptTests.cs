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
}
