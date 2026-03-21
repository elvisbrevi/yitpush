using System.Diagnostics;
using System.Text;

namespace YitPush;

partial class Program
{
    private static string ParseLanguage(string[] args)
    {
        for (int i = 0; i < args.Length; i++)
        {
            if ((args[i] == "--language" || args[i] == "--lang" || args[i] == "-l") && i + 1 < args.Length && !args[i + 1].StartsWith("-"))
                return args[i + 1];
            if (args[i].StartsWith("--language=") || args[i].StartsWith("--lang=") || args[i].StartsWith("-l="))
                return args[i].Split('=', 2)[1];
        }
        return "english";
    }

    private static async Task<bool> IsGitRepository()
    {
        try
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "git",
                    Arguments = "rev-parse --git-dir",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();
            await process.WaitForExitAsync();
            return process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    private static async Task<string> GetGitDiff()
    {
        try
        {
            // First, stage all changes (including untracked files) to get a complete diff
            var addProcess = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "git",
                    Arguments = "add -A --dry-run",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };
            addProcess.Start();
            await addProcess.WaitForExitAsync();

            // Get diff including untracked files
            // First try to get already staged changes
            var stagedProcess = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "git",
                    Arguments = "diff --cached",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };
            stagedProcess.Start();
            var stagedOutput = await stagedProcess.StandardOutput.ReadToEndAsync();
            await stagedProcess.WaitForExitAsync();

            // Get unstaged changes in tracked files
            var unstagedProcess = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "git",
                    Arguments = "diff",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };
            unstagedProcess.Start();
            var unstagedOutput = await unstagedProcess.StandardOutput.ReadToEndAsync();
            await unstagedProcess.WaitForExitAsync();

            // Get untracked files
            var untrackedProcess = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "git",
                    Arguments = "ls-files --others --exclude-standard",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };
            untrackedProcess.Start();
            var untrackedFiles = await untrackedProcess.StandardOutput.ReadToEndAsync();
            await untrackedProcess.WaitForExitAsync();

            var output = new StringBuilder();

            if (!string.IsNullOrWhiteSpace(stagedOutput))
            {
                output.AppendLine("=== Staged Changes ===");
                output.AppendLine(stagedOutput);
                output.AppendLine();
            }

            if (!string.IsNullOrWhiteSpace(unstagedOutput))
            {
                output.AppendLine("=== Unstaged Changes ===");
                output.AppendLine(unstagedOutput);
                output.AppendLine();
            }

            if (!string.IsNullOrWhiteSpace(untrackedFiles))
            {
                output.AppendLine("=== New Files (Untracked) ===");
                output.AppendLine(untrackedFiles);
            }

            return output.ToString();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error getting git diff: {ex.Message}");
            return string.Empty;
        }
    }

    private static async Task<string?> GetCurrentBranch()
    {
        var result = await RunGitOutput("branch --show-current");
        return string.IsNullOrWhiteSpace(result) ? null : result;
    }

    private static async Task<bool> ExecuteGitPush(bool requireConfirmation, string? remoteName = null)
    {
        // First try regular push
        var pushArgs = remoteName != null ? $"push {remoteName}" : "push";
        Console.WriteLine($"   git {pushArgs}");
        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "git",
                Arguments = pushArgs,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        process.Start();
        var output = await process.StandardOutput.ReadToEndAsync();
        var error = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();

        if (process.ExitCode == 0)
        {
            return true;
        }

        // Check if error indicates no upstream branch
        if (!string.IsNullOrEmpty(error) && (error.Contains("no upstream branch") || error.Contains("has no upstream branch")))
        {
            var upstreamRemote = remoteName ?? "origin";

            // In automatic mode, set upstream automatically
            if (!requireConfirmation)
            {
                var currentBranch = await GetCurrentBranch();
                if (currentBranch != null)
                {
                    Console.WriteLine($"   git push --set-upstream {upstreamRemote} {currentBranch}");
                    var upstreamProcess = new Process
                    {
                        StartInfo = new ProcessStartInfo
                        {
                            FileName = "git",
                            Arguments = $"push --set-upstream {upstreamRemote} {currentBranch}",
                            RedirectStandardOutput = true,
                            RedirectStandardError = true,
                            UseShellExecute = false,
                            CreateNoWindow = true
                        }
                    };

                    upstreamProcess.Start();
                    await upstreamProcess.StandardOutput.ReadToEndAsync();
                    var upstreamError = await upstreamProcess.StandardError.ReadToEndAsync();
                    await upstreamProcess.WaitForExitAsync();

                    if (upstreamProcess.ExitCode == 0)
                    {
                        return true;
                    }
                    else
                    {
                        Console.WriteLine($"Git error: {upstreamError}");
                        return false;
                    }
                }
            }
            else
            {
                // In confirmation mode, ask user if they want to set upstream
                if (!string.IsNullOrEmpty(error))
                {
                    Console.WriteLine($"Git error: {error}");
                }

                var currentBranch = await GetCurrentBranch();
                if (currentBranch != null)
                {
                    Console.WriteLine($"\nThe current branch '{currentBranch}' has no upstream branch.");
                    Console.Write($"Do you want to push and set upstream to {upstreamRemote}/{currentBranch}? (y/n): ");
                    var response = Console.ReadLine()?.Trim().ToLower();

                    if (response == "y" || response == "yes")
                    {
                        Console.WriteLine($"   git push --set-upstream {upstreamRemote} {currentBranch}");
                        var upstreamProcess = new Process
                        {
                            StartInfo = new ProcessStartInfo
                            {
                                FileName = "git",
                                Arguments = $"push --set-upstream {upstreamRemote} {currentBranch}",
                                RedirectStandardOutput = true,
                                RedirectStandardError = true,
                                UseShellExecute = false,
                                CreateNoWindow = true
                            }
                        };

                        upstreamProcess.Start();
                        await upstreamProcess.StandardOutput.ReadToEndAsync();
                        var upstreamError = await upstreamProcess.StandardError.ReadToEndAsync();
                        await upstreamProcess.WaitForExitAsync();

                        if (upstreamProcess.ExitCode == 0)
                        {
                            return true;
                        }
                        else
                        {
                            Console.WriteLine($"Git error: {upstreamError}");
                            return false;
                        }
                    }
                    else
                    {
                        Console.WriteLine("Push cancelled.");
                        return false;
                    }
                }
                else
                {
                    Console.WriteLine("\nNote: The current branch has no upstream branch.");
                    Console.WriteLine("      You can set it with: git push --set-upstream origin <branch-name>");
                    return false;
                }
            }
        }

        if (!string.IsNullOrEmpty(error))
        {
            Console.WriteLine($"Git error: {error}");
        }
        return false;
    }

    private static async Task<bool> ExecuteGitCommand(string arguments)
    {
        try
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "git",
                    Arguments = arguments,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();
            var output = await process.StandardOutput.ReadToEndAsync();
            var error = await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();

            if (!string.IsNullOrWhiteSpace(error) && process.ExitCode != 0)
            {
                Console.WriteLine($"Git error: {error}");
            }

            return process.ExitCode == 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error executing git command: {ex.Message}");
            return false;
        }
    }

    private static async Task<string> RunGitOutput(string arguments)
    {
        try
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "git",
                    Arguments = arguments,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };
            process.Start();
            var output = await process.StandardOutput.ReadToEndAsync();
            await process.WaitForExitAsync();
            return process.ExitCode == 0 ? output.Trim() : string.Empty;
        }
        catch { return string.Empty; }
    }

    private static async Task<bool> ExecuteGitCommitWithMessage(string commitMessage)
    {
        string tempFile = Path.Combine(Path.GetTempPath(), $"git_commit_msg_{Guid.NewGuid():N}.txt");
        try
        {
            await File.WriteAllTextAsync(tempFile, commitMessage, Encoding.UTF8);
            return await ExecuteGitCommand($"commit -F \"{tempFile}\"");
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    private static async Task<List<(string Name, string Type, string Date)>> GetGitBranches()
    {
        var branches = new List<(string Name, string Type, string Date)>();
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "git",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            psi.ArgumentList.Add("branch");
            psi.ArgumentList.Add("-a");
            psi.ArgumentList.Add("--sort=-committerdate");
            psi.ArgumentList.Add("--format=%(refname:short)|%(committerdate:format:%Y-%m-%d %H:%M)|%(refname)");
            var process = new Process { StartInfo = psi };

            process.Start();
            var output = await process.StandardOutput.ReadToEndAsync();
            await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();

            if (process.ExitCode == 0 && !string.IsNullOrWhiteSpace(output))
            {
                foreach (var line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;

                    var parts = line.Split('|', 3);
                    if (parts.Length < 3) continue;

                    var name = parts[0].Trim();
                    var date = parts[1].Trim();
                    var refname = parts[2].Trim();

                    // Skip HEAD pointers (e.g. refs/remotes/origin/HEAD)
                    if (refname.Contains("/HEAD", StringComparison.OrdinalIgnoreCase)) continue;

                    var type = refname.StartsWith("refs/remotes/") ? "remote" : "local";
                    branches.Add((name, type, date));
                }
            }
        }
        catch { }

        return branches;
    }

    private static async Task<string> GetBranchDiff(string fromBranch, string toBranch)
    {
        return await RunGitOutput($"diff {toBranch}...{fromBranch}");
    }
}
