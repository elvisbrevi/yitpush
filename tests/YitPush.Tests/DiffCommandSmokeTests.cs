using System.Diagnostics;
using YitPush;

namespace YitPush.Tests;

public class DiffCommandSmokeTests : IDisposable
{
    private readonly string _repoDir;
    private readonly string _originalDir;

    public DiffCommandSmokeTests()
    {
        _originalDir = Directory.GetCurrentDirectory();
        _repoDir = Path.Combine(Path.GetTempPath(), "yp-diff-smoke-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_repoDir);
        Directory.SetCurrentDirectory(_repoDir);

        // Initialize a fresh git repo with a baseline commit.
        RunGit("init -q -b main");
        RunGit("config user.email smoke@test.local");
        RunGit("config user.name smoke");
        File.WriteAllText(Path.Combine(_repoDir, "hello.txt"), "line one\nline two\nline three\n");
        RunGit("add .");
        RunGit("commit -q -m initial");
    }

    public void Dispose()
    {
        Directory.SetCurrentDirectory(_originalDir);
        try { Directory.Delete(_repoDir, recursive: true); } catch { /* best-effort */ }
    }

    [Fact]
    public void Working_tree_diff_round_trips_through_parser_with_expected_file()
    {
        // Modify the tracked file, then parse the real `git diff HEAD` output.
        File.WriteAllText(Path.Combine(_repoDir, "hello.txt"),
            "line one\nline two (renamed)\nline two-and-a-half\nline three\n");

        var raw = RunGitCapture("diff HEAD --no-color");
        var files = Program.ParseUnifiedDiff(raw);

        var file = Assert.Single(files);
        Assert.Equal("hello.txt", file.Path);
        Assert.Equal(2, file.Additions);
        Assert.Equal(1, file.Deletions);
    }

    [Fact]
    public void Branch_to_branch_diff_returns_empty_for_identical_branches()
    {
        // No changes between HEAD and main on a fresh repo.
        var raw = RunGitCapture("diff HEAD..main --no-color");
        var files = Program.ParseUnifiedDiff(raw);

        Assert.Empty(files);
    }

    [Fact]
    public void Working_tree_diff_for_a_new_untracked_file_is_not_in_git_diff_HEAD()
    {
        // A bare `git diff HEAD` does not include untracked files; the test
        // documents the integration boundary: untracked files require a separate
        // `git add` step, and the parser faithfully reflects that.
        File.WriteAllText(Path.Combine(_repoDir, "fresh.txt"), "brand new\n");

        var raw = RunGitCapture("diff HEAD --no-color");
        var files = Program.ParseUnifiedDiff(raw);

        Assert.DoesNotContain(files, f => f.Path == "fresh.txt");
    }

    private void RunGit(string args)
    {
        var psi = new ProcessStartInfo("git", args)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        using var p = Process.Start(psi)!;
        p.WaitForExit();
        Assert.True(p.ExitCode == 0, $"git {args} failed: {p.StandardError.ReadToEnd()}");
    }

    private string RunGitCapture(string args)
    {
        var psi = new ProcessStartInfo("git", args)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        using var p = Process.Start(psi)!;
        var output = p.StandardOutput.ReadToEnd();
        p.WaitForExit();
        return output;
    }
}
