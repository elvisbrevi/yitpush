using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Spectre.Console;

namespace YitPush;

class Program
{
    private const string DeepSeekApiUrl = "https://api.deepseek.com/v1/chat/completions";
    private const string DeepSeekModel = "deepseek-reasoner";

    static async Task<int> Main(string[] args)
    {
        // Configure console encoding for Unicode support (especially Windows PowerShell)
        try
        {
            Console.OutputEncoding = Encoding.UTF8;
            Console.InputEncoding = Encoding.UTF8;
        }
        catch
        {
            // Ignore encoding errors - console may not support UTF-8
        }

    Console.WriteLine("🚀 YitPush - AI-Powered Git Commit Tool\n");

    // Process command line arguments
    bool requireConfirmation = args.Contains("--confirm");
    bool showHelp = args.Contains("--help");
    bool detailed = args.Contains("--detailed");
    bool createAzureRepo = args.Contains("--new-repo-azure");
    bool prDescription = args.Contains("--pr-description");
    bool save = args.Contains("--save");
    string language = "english"; // default language
    
    // Parse language flag (supports --language es, --lang es, --language=es, --lang=es)
    for (int i = 0; i < args.Length; i++)
    {
        if (args[i] == "--language" || args[i] == "--lang")
        {
            if (i + 1 < args.Length && !args[i + 1].StartsWith("--"))
            {
                language = args[i + 1];
            }
            break;
        }
        else if (args[i].StartsWith("--language=") || args[i].StartsWith("--lang="))
        {
            language = args[i].Split('=', 2)[1];
            break;
        }
    }
    
    if (showHelp)
    {
        Console.WriteLine("Usage: yitpush [options]");
        Console.WriteLine();
        Console.WriteLine("Options:");
        Console.WriteLine("  --confirm    Ask for confirmation before committing");
        Console.WriteLine("  --detailed        Generate detailed commit with body (title + paragraphs + bullet points)");
        Console.WriteLine("  --language        Set output language for commit message (e.g., 'english', 'spanish', 'french')");
        Console.WriteLine("  --new-repo-azure  Create a new Azure DevOps repository interactively");
        Console.WriteLine("  --pr-description  Generate a PR description by comparing two branches");
        Console.WriteLine("  --save            Save the output to a markdown file");
        Console.WriteLine("  --help            Show this help message");
        Console.WriteLine();
        Console.WriteLine("By default, YitPush will automatically commit and push without confirmation.");
        Console.WriteLine("Use --confirm if you want to review the commit message before proceeding.");
        Console.WriteLine("Use --detailed for detailed commit messages with full explanations.");
        Console.WriteLine("Use --language to specify the output language (default: english).");
        Console.WriteLine("Use --pr-description to generate a PR description (combinable with --lang, --detailed and --save).");
        Console.WriteLine("Use --save to save the output to a markdown file (works with default and --pr-description modes).");
        Console.WriteLine();
        return 0;
    }

    try
        {
            // Check if we're in a git repository
            if (!await IsGitRepository())
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("❌ Error: Not a git repository.");
                Console.ResetColor();
                Console.WriteLine("\nPlease run this command from within a git repository.");
                return 1;
            }

            // PR description mode - separate flow
            if (prDescription)
            {
                return await GeneratePrDescription(detailed, language, save);
            }

            // Create Azure DevOps repository if requested
            string? targetRemote = null;
            if (createAzureRepo)
            {
                targetRemote = await CreateAzureDevOpsRepo();
                if (targetRemote == null)
                {
                    return 1;
                }
                Console.WriteLine();
            }

            // Get API key from environment variable
            var apiKey = Environment.GetEnvironmentVariable("DEEPSEEK_API_KEY");
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("❌ Error: DEEPSEEK_API_KEY environment variable not found.");
                Console.ResetColor();
                Console.WriteLine("\nPlease set your DeepSeek API key:");
                Console.WriteLine("  export DEEPSEEK_API_KEY='your-api-key-here'");
                return 1;
            }

            // Get git diff
            Console.WriteLine("📊 Analyzing git changes...");
            var diff = await GetGitDiff();

            bool hasChanges = !string.IsNullOrWhiteSpace(diff);
            
            if (hasChanges)
            {
                Console.WriteLine($"Found changes ({diff.Length} characters)\n");
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("⚠️  No changes detected in the repository.");
                Console.ResetColor();
                Console.WriteLine("\nWill attempt to push existing commits...\n");
            }

            string? commitMessage = null;
            
            if (hasChanges)
            {
                // Get commit message from DeepSeek
                Console.WriteLine($"🤖 Generating commit message with DeepSeek Reasoning...{(detailed ? " (detailed mode)" : "")}");
                commitMessage = await GenerateCommitMessage(apiKey, diff, detailed, language);

                if (string.IsNullOrWhiteSpace(commitMessage))
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("❌ Error: Failed to generate commit message.");
                    Console.ResetColor();
                    return 1;
                }

                Console.WriteLine("\n📝 Generated commit message:");
                AnsiConsole.Write(new Panel(commitMessage)
                    .Header("Commit Message")
                    .BorderColor(Color.Cyan1)
                    .Padding(1, 0));
                Console.WriteLine();

                if (save)
                {
                    var fileName = $"commit-message-{DateTime.Now:yyyyMMdd-HHmmss}.md";
                    await File.WriteAllTextAsync(fileName, commitMessage);
                    AnsiConsole.MarkupLine($"[green]✅ Commit message saved to:[/] {fileName}\n");
                }

                // Confirm with user if --confirm flag is set
                if (requireConfirmation)
                {
                    Console.Write("Do you want to proceed with this commit? (y/n): ");
                    var response = Console.ReadLine()?.Trim().ToLower();

                    if (response != "y" && response != "yes")
                    {
                        Console.WriteLine("\n❌ Commit cancelled.");
                        return 0;
                    }
                }
                else
                {
                    Console.WriteLine("⏩ Proceeding automatically (use --confirm to review)...");
                }
            }
            else
            {
                // No changes, just push - confirm if in confirmation mode
                if (requireConfirmation)
                {
                    Console.Write("No changes to commit. Do you want to push existing commits? (y/n): ");
                    var response = Console.ReadLine()?.Trim().ToLower();

                    if (response != "y" && response != "yes")
                    {
                        Console.WriteLine("\n❌ Push cancelled.");
                        return 0;
                    }
                }
                else
                {
                    Console.WriteLine("⏩ No changes to commit, proceeding with push...");
                }
            }

            // Execute git commands
            Console.WriteLine("\n⚙️  Executing git commands...");

            if (hasChanges)
            {
                // git add .
                Console.WriteLine("   git add .");
                if (!await ExecuteGitCommand("add ."))
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("❌ Error: git add failed.");
                    Console.ResetColor();
                    return 1;
                }

                // git commit
                Console.WriteLine($"   git commit -m \"{commitMessage}\"");
                if (!await ExecuteGitCommand($"commit -m \"{commitMessage}\""))
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("❌ Error: git commit failed.");
                    Console.ResetColor();
                    return 1;
                }
            }

            // git push
            if (!await ExecuteGitPush(requireConfirmation, targetRemote))
            {
                return 1;
            }

            Console.ForegroundColor = ConsoleColor.Green;
            if (hasChanges)
            {
                Console.WriteLine("\n✅ Successfully committed and pushed changes!");
            }
            else
            {
                Console.WriteLine("\n✅ Successfully pushed changes!");
            }
            Console.ResetColor();

            return 0;
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"\n❌ Unexpected error: {ex.Message}");
            Console.ResetColor();
            return 1;
        }
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

    private static async Task<string> GenerateCommitMessage(string apiKey, string diff, bool detailed = false, string language = "english")
    {
        const int maxRetries = 3;
        const int baseDelayMs = 1000;
        const int deepseekMaxContextTokens = 131072;
        const int maxCompletionTokens = 8000;
        const int reservedTokens = maxCompletionTokens + 5000;
        const int maxPromptTokens = deepseekMaxContextTokens - reservedTokens;
        const int averageCharsPerToken = 4;
        const int maxPromptChars = maxPromptTokens * averageCharsPerToken;


        diff = TruncateDiff(diff, maxPromptChars);

        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                using var httpClient = new HttpClient
                {
                    Timeout = TimeSpan.FromSeconds(120) // Increased timeout for reasoning models
                };
                httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");

                string prompt;
                if (detailed)
                {
                    prompt = $@"You are a git commit message expert. Based on the following git diff, generate a detailed conventional commit with title and body.

LANGUAGE: Write the commit message in {language}.

FORMAT REQUIREMENTS:
1. TITLE LINE (required):
   - Start with conventional commit type: feat, fix, docs, style, refactor, test, chore
   - Use imperative mood: 'add' not 'added' or 'adds'
   - Max 50 characters for the subject line
   - Example: 'feat: add user authentication with JWT'

2. BODY (required for detailed mode):
   - Add one blank line after title
   - Include 1-2 descriptive paragraphs explaining the changes
   - Add bullet points for key changes (use '-' or '*')
   - Mention important files modified or added
   - Keep lines under 72 characters for git compatibility
   - Focus on the 'why' not just the 'what'

3. STYLE:
    - Write in clear, professional {language}
   - Use present tense for changes
   - Be specific about technical implementation

Git diff:
{diff}

Generate the complete commit message (title + body):";
                }
                else
                {
                    prompt = $@"You are a git commit message expert. Based on the following git diff, generate a concise, clear, and descriptive commit message following conventional commits format.

LANGUAGE: Write the commit message in {language}.

The commit message should:
- Start with a type (feat, fix, docs, style, refactor, test, chore)
- Be in imperative mood (e.g., 'add' not 'added' or 'adds')
- Be concise but descriptive (50 characters or less for the subject)
- Not include any extra formatting, quotes, or explanations
- Just return the commit message itself, nothing else

Git diff:
{diff}

Generate only the commit message:";
                }

                var requestBody = new
                {
                    model = DeepSeekModel,
                    messages = new[]
                    {
                        new { role = "user", content = prompt }
                    },
                    max_tokens = 8000  // Increased for reasoning models to complete both reasoning and final answer
                };

                var jsonContent = JsonSerializer.Serialize(requestBody);
                var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

                var response = await httpClient.PostAsync(DeepSeekApiUrl, content);

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    Console.WriteLine($"API Error (attempt {attempt}/{maxRetries}): {response.StatusCode}");
                    Console.WriteLine($"Response: {errorContent}");

                    // If it's a rate limit or server error, retry
                    if (attempt < maxRetries && ((int)response.StatusCode == 429 || (int)response.StatusCode >= 500))
                    {
                        var delay = baseDelayMs * (int)Math.Pow(2, attempt - 1);
                        Console.WriteLine($"Retrying in {delay}ms...");
                        await Task.Delay(delay);
                        continue;
                    }

                    return string.Empty;
                }

                var responseJson = await response.Content.ReadAsStringAsync();
                var apiResponse = JsonSerializer.Deserialize<DeepSeekResponse>(responseJson);

                if (apiResponse?.Choices == null || apiResponse.Choices.Length == 0)
                {
                    Console.WriteLine($"No response from API (attempt {attempt}/{maxRetries})");

                    if (attempt < maxRetries)
                    {
                        var delay = baseDelayMs * (int)Math.Pow(2, attempt - 1);
                        Console.WriteLine($"Retrying in {delay}ms...");
                        await Task.Delay(delay);
                        continue;
                    }

                    return string.Empty;
                }

                var message = apiResponse.Choices[0].Message?.Content?.Trim() ?? string.Empty;

                // Clean up the message (remove quotes if present)
                message = message.Trim('"', '\'', ' ', '\n', '\r');

                return message;
            }
            catch (TaskCanceledException ex)
            {
                Console.WriteLine($"Request timeout (attempt {attempt}/{maxRetries}): {ex.Message}");

                if (attempt < maxRetries)
                {
                    var delay = baseDelayMs * (int)Math.Pow(2, attempt - 1);
                    Console.WriteLine($"Retrying in {delay}ms...");
                    await Task.Delay(delay);
                    continue;
                }

                return string.Empty;
            }
            catch (HttpRequestException ex)
            {
                Console.WriteLine($"Network error (attempt {attempt}/{maxRetries}): {ex.Message}");

                if (attempt < maxRetries)
                {
                    var delay = baseDelayMs * (int)Math.Pow(2, attempt - 1);
                    Console.WriteLine($"Retrying in {delay}ms...");
                    await Task.Delay(delay);
                    continue;
                }

                return string.Empty;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error calling DeepSeek API (attempt {attempt}/{maxRetries}): {ex.Message}");

                if (attempt < maxRetries)
                {
                    var delay = baseDelayMs * (int)Math.Pow(2, attempt - 1);
                    Console.WriteLine($"Retrying in {delay}ms...");
                    await Task.Delay(delay);
                    continue;
                }

                return string.Empty;
            }
        }

        return string.Empty;
    }

    private static string TruncateDiff(string diff, int maxChars)
    {
        if (string.IsNullOrEmpty(diff) || diff.Length <= maxChars)
        {
            return diff;
        }

        Console.WriteLine($"\n⚠️  Diff is too large ({diff.Length} chars). Truncating to {maxChars} chars...");

        int keepBeginning = maxChars / 2;
        int keepEnd = maxChars - keepBeginning;
        string beginning = diff.Substring(0, keepBeginning);
        string end = diff.Substring(diff.Length - keepEnd);

        return $"{beginning}\n\n...[TRUNCATED - {diff.Length - maxChars} characters omitted]...\n\n{end}";
    }

    private static async Task<string?> GetCurrentBranch()
    {
        try
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "git",
                    Arguments = "branch --show-current",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();
            var output = await process.StandardOutput.ReadToEndAsync();
            await process.StandardError.ReadToEndAsync(); // discard
            await process.WaitForExitAsync();

            if (process.ExitCode == 0)
            {
                return output.Trim();
            }
            else
            {
                return null;
            }
        }
        catch
        {
            return null;
        }
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
        try
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "git",
                    Arguments = $"diff {toBranch}...{fromBranch}",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();
            var output = await process.StandardOutput.ReadToEndAsync();
            await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();

            return process.ExitCode == 0 ? output : string.Empty;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error getting branch diff: {ex.Message}");
            return string.Empty;
        }
    }

    private static async Task<string> GeneratePrDescriptionContent(string apiKey, string diff, bool detailed, string language)
    {
        const int maxRetries = 3;
        const int baseDelayMs = 1000;
        const int deepseekMaxContextTokens = 131072;
        const int maxCompletionTokens = 8000;
        const int reservedTokens = maxCompletionTokens + 5000;
        const int maxPromptTokens = deepseekMaxContextTokens - reservedTokens;
        const int averageCharsPerToken = 4;
        const int maxPromptChars = maxPromptTokens * averageCharsPerToken;

        diff = TruncateDiff(diff, maxPromptChars);

        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                using var httpClient = new HttpClient
                {
                    Timeout = TimeSpan.FromSeconds(120)
                };
                httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");

                string prompt;
                if (detailed)
                {
                    prompt = $@"You are a pull request description expert. Based on the following git diff between two branches, generate a detailed pull request description in Markdown format.

LANGUAGE: Write the description in {language}.

FORMAT REQUIREMENTS:
1. TITLE: A concise PR title (conventional commit style)
2. SUMMARY: A clear paragraph explaining the overall purpose of the changes
3. CHANGES: A detailed bullet list of all changes grouped by category (features, fixes, refactoring, etc.)
4. FILES CHANGED: List the key files modified and what was changed in each
5. TESTING: Suggestions for testing the changes
6. NOTES: Any additional notes, breaking changes, or migration steps if applicable

STYLE:
- Write in clear, professional {language}
- Use proper Markdown formatting (headers, lists, code blocks)
- Be thorough and cover all changes in the diff
- Highlight breaking changes if any

Git diff:
{diff}

Generate the complete pull request description in Markdown:";
                }
                else
                {
                    prompt = $@"You are a pull request description expert. Based on the following git diff between two branches, generate a concise pull request description in Markdown format.

LANGUAGE: Write the description in {language}.

FORMAT REQUIREMENTS:
1. TITLE: A concise PR title (conventional commit style)
2. SUMMARY: A brief paragraph explaining the purpose of the changes
3. CHANGES: A bullet list of the key changes

STYLE:
- Write in clear, professional {language}
- Use proper Markdown formatting (headers, lists)
- Be concise but cover the important changes
- Just return the Markdown content, nothing else

Git diff:
{diff}

Generate the pull request description in Markdown:";
                }

                var requestBody = new
                {
                    model = DeepSeekModel,
                    messages = new[]
                    {
                        new { role = "user", content = prompt }
                    },
                    max_tokens = 8000
                };

                var jsonContent = JsonSerializer.Serialize(requestBody);
                var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

                var response = await httpClient.PostAsync(DeepSeekApiUrl, content);

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    Console.WriteLine($"API Error (attempt {attempt}/{maxRetries}): {response.StatusCode}");
                    Console.WriteLine($"Response: {errorContent}");

                    if (attempt < maxRetries && ((int)response.StatusCode == 429 || (int)response.StatusCode >= 500))
                    {
                        var delay = baseDelayMs * (int)Math.Pow(2, attempt - 1);
                        Console.WriteLine($"Retrying in {delay}ms...");
                        await Task.Delay(delay);
                        continue;
                    }

                    return string.Empty;
                }

                var responseJson = await response.Content.ReadAsStringAsync();
                var apiResponse = JsonSerializer.Deserialize<DeepSeekResponse>(responseJson);

                if (apiResponse?.Choices == null || apiResponse.Choices.Length == 0)
                {
                    Console.WriteLine($"No response from API (attempt {attempt}/{maxRetries})");

                    if (attempt < maxRetries)
                    {
                        var delay = baseDelayMs * (int)Math.Pow(2, attempt - 1);
                        Console.WriteLine($"Retrying in {delay}ms...");
                        await Task.Delay(delay);
                        continue;
                    }

                    return string.Empty;
                }

                var message = apiResponse.Choices[0].Message?.Content?.Trim() ?? string.Empty;
                message = message.Trim('"', '\'', ' ', '\n', '\r');

                return message;
            }
            catch (TaskCanceledException ex)
            {
                Console.WriteLine($"Request timeout (attempt {attempt}/{maxRetries}): {ex.Message}");

                if (attempt < maxRetries)
                {
                    var delay = baseDelayMs * (int)Math.Pow(2, attempt - 1);
                    Console.WriteLine($"Retrying in {delay}ms...");
                    await Task.Delay(delay);
                    continue;
                }

                return string.Empty;
            }
            catch (HttpRequestException ex)
            {
                Console.WriteLine($"Network error (attempt {attempt}/{maxRetries}): {ex.Message}");

                if (attempt < maxRetries)
                {
                    var delay = baseDelayMs * (int)Math.Pow(2, attempt - 1);
                    Console.WriteLine($"Retrying in {delay}ms...");
                    await Task.Delay(delay);
                    continue;
                }

                return string.Empty;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error calling DeepSeek API (attempt {attempt}/{maxRetries}): {ex.Message}");

                if (attempt < maxRetries)
                {
                    var delay = baseDelayMs * (int)Math.Pow(2, attempt - 1);
                    Console.WriteLine($"Retrying in {delay}ms...");
                    await Task.Delay(delay);
                    continue;
                }

                return string.Empty;
            }
        }

        return string.Empty;
    }

    private static async Task<int> GeneratePrDescription(bool detailed, string language, bool save)
    {
        AnsiConsole.MarkupLine("[bold blue]📋 PR Description Generator[/]\n");

        // Get API key
        var apiKey = Environment.GetEnvironmentVariable("DEEPSEEK_API_KEY");
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("❌ Error: DEEPSEEK_API_KEY environment variable not found.");
            Console.ResetColor();
            Console.WriteLine("\nPlease set your DeepSeek API key:");
            Console.WriteLine("  export DEEPSEEK_API_KEY='your-api-key-here'");
            return 1;
        }

        // Get branches
        AnsiConsole.MarkupLine("🔍 Fetching branches...");
        var branches = await GetGitBranches();

        if (branches.Count < 2)
        {
            AnsiConsole.MarkupLine("[red]❌ Need at least 2 branches to compare.[/]");
            return 1;
        }

        // Build display strings with columns: name | type | date
        var maxNameLen = branches.Max(b => b.Name.Length);
        var displayMap = new Dictionary<string, string>(); // display -> branch name
        var displayList = new List<string>();

        foreach (var b in branches)
        {
            var display = $"{b.Name.PadRight(maxNameLen + 2)} {b.Type.PadRight(8)} {b.Date}";
            displayMap[display] = b.Name;
            displayList.Add(display);
        }

        // Select source branch (from - the branch with changes)
        var fromDisplay = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("📋 Select [green]source[/] branch (branch with changes):")
                .PageSize(15)
                .HighlightStyle(new Style(Color.Cyan1))
                .AddChoices(displayList));

        var fromBranch = displayMap[fromDisplay];
        AnsiConsole.MarkupLine($"\n[green]✅ Source branch:[/] {fromBranch}");

        // Select target branch (to - the branch to merge into)
        var targetDisplayList = displayList.Where(d => displayMap[d] != fromBranch).ToList();
        var toDisplay = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("📋 Select [green]target[/] branch (branch to merge into):")
                .PageSize(15)
                .HighlightStyle(new Style(Color.Cyan1))
                .AddChoices(targetDisplayList));

        var toBranch = displayMap[toDisplay];
        AnsiConsole.MarkupLine($"\n[green]✅ Target branch:[/] {toBranch}");

        // Get diff between branches
        AnsiConsole.MarkupLine($"\n📊 Analyzing differences between [cyan]{fromBranch}[/] and [cyan]{toBranch}[/]...");
        var diff = await GetBranchDiff(fromBranch, toBranch);

        if (string.IsNullOrWhiteSpace(diff))
        {
            AnsiConsole.MarkupLine("[yellow]⚠️  No differences found between the selected branches.[/]");
            return 0;
        }

        AnsiConsole.MarkupLine($"Found differences ({diff.Length} characters)\n");

        // Generate PR description
        AnsiConsole.MarkupLine($"🤖 Generating PR description with DeepSeek Reasoning...{(detailed ? " (detailed mode)" : "")}");
        var description = await GeneratePrDescriptionContent(apiKey, diff, detailed, language);

        if (string.IsNullOrWhiteSpace(description))
        {
            AnsiConsole.MarkupLine("[red]❌ Error: Failed to generate PR description.[/]");
            return 1;
        }

        AnsiConsole.WriteLine();
        AnsiConsole.Write(new Panel(description)
            .Header("PR Description")
            .BorderColor(Color.Cyan1)
            .Padding(1, 0));

        if (save)
        {
            var fileName = $"pr-description-{fromBranch.Replace("/", "-")}-to-{toBranch.Replace("/", "-")}.md";
            await File.WriteAllTextAsync(fileName, description);
            AnsiConsole.MarkupLine($"\n[green]✅ PR description saved to:[/] {fileName}");
        }

        return 0;
    }

    private static async Task<string?> CreateAzureDevOpsRepo()
    {
        AnsiConsole.MarkupLine("[bold blue]☁️  Azure DevOps - Create New Repository[/]\n");

        // 1. Check if az CLI is installed
        var azCheck = await RunCommandCapture("az", "--version");
        if (azCheck == null)
        {
            AnsiConsole.MarkupLine("[red]❌ Azure CLI (az) is not installed.[/]");
            AnsiConsole.MarkupLine("\nInstall it from: https://docs.microsoft.com/en-us/cli/azure/install-azure-cli");
            return null;
        }

        // 2. Check if azure-devops extension is installed
        AnsiConsole.MarkupLine("🔍 Checking Azure DevOps extension...");
        var extCheck = await RunCommandCapture("az", "extension show --name azure-devops --output json");
        if (extCheck == null)
        {
            AnsiConsole.MarkupLine("📦 Installing Azure DevOps extension...");
            var installResult = await RunCommandPassthrough("az", "extension add --name azure-devops");
            if (!installResult)
            {
                AnsiConsole.MarkupLine("[red]❌ Failed to install Azure DevOps extension.[/]");
                return null;
            }
        }
        AnsiConsole.MarkupLine("[green]✅ Azure DevOps extension available.[/]\n");

        // 3. Check if logged in
        var accountJson = await RunCommandCapture("az", "account show --output json");
        if (accountJson == null)
        {
            AnsiConsole.MarkupLine("🔐 Not logged into Azure. Starting interactive login...\n");
            var loginResult = await RunCommandPassthrough("az", "login");
            if (!loginResult)
            {
                AnsiConsole.MarkupLine("[red]❌ Azure login failed.[/]");
                return null;
            }
            AnsiConsole.MarkupLine("\n[green]✅ Successfully logged into Azure.[/]\n");
        }
        else
        {
            try
            {
                using var doc = JsonDocument.Parse(accountJson);
                var userName = doc.RootElement.TryGetProperty("user", out var userProp)
                    && userProp.TryGetProperty("name", out var nameProp)
                    ? nameProp.GetString() : "unknown";
                AnsiConsole.MarkupLine($"[green]✅ Logged into Azure as:[/] {userName}\n");
            }
            catch
            {
                AnsiConsole.MarkupLine("[green]✅ Already logged into Azure.[/]\n");
            }
        }

        // 4. List organizations via Azure DevOps REST API
        var organizations = await FetchAzureOrganizations();

        if (organizations.Count == 0)
        {
            AnsiConsole.MarkupLine("[red]❌ No Azure DevOps organizations found for this account.[/]");
            return null;
        }

        organizations.Sort(StringComparer.OrdinalIgnoreCase);
        var selectedOrg = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("📋 Select [green]organization[/]:")
                .PageSize(10)
                .HighlightStyle(new Style(Color.Cyan1))
                .AddChoices(organizations));

        var orgUrl = $"https://dev.azure.com/{selectedOrg}";
        AnsiConsole.MarkupLine($"\n[green]✅ Organization:[/] {selectedOrg}");

        // 5. List projects
        AnsiConsole.MarkupLine("\n📋 Fetching projects...");
        var projects = await FetchAzureProjects(orgUrl);

        if (projects.Count == 0)
        {
            AnsiConsole.MarkupLine("[red]❌ No projects found in this organization.[/]");
            return null;
        }

        projects.Sort(StringComparer.OrdinalIgnoreCase);
        var selectedProject = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("📋 Select [green]project[/]:")
                .PageSize(10)
                .HighlightStyle(new Style(Color.Cyan1))
                .AddChoices(projects));

        AnsiConsole.MarkupLine($"\n[green]✅ Project:[/] {selectedProject}");

        // 6. Repository name (suggest current directory name as default)
        var currentDirName = new DirectoryInfo(Directory.GetCurrentDirectory()).Name;
        var repoName = AnsiConsole.Prompt(
            new TextPrompt<string>("📝 Repository name:")
                .DefaultValue(currentDirName)
                .PromptStyle(new Style(Color.Cyan1)));

        // 7. Check if repo already exists
        AnsiConsole.MarkupLine($"\n🔍 Checking if repository '[cyan]{repoName}[/]' already exists...");
        var existingRepoJson = await RunCommandCapture("az",
            $"repos show --repository \"{repoName}\" --organization {orgUrl} --project \"{selectedProject}\" --output json");

        string? remoteUrl = null;

        if (existingRepoJson != null)
        {
            try
            {
                using var doc = JsonDocument.Parse(existingRepoJson);
                if (doc.RootElement.TryGetProperty("remoteUrl", out var urlProp))
                {
                    remoteUrl = urlProp.GetString();
                }
            }
            catch { }

            AnsiConsole.MarkupLine($"[yellow]⚠️  Repository '{repoName}' already exists:[/] {remoteUrl}");
            var useExisting = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title("What do you want to do?")
                    .HighlightStyle(new Style(Color.Cyan1))
                    .AddChoices(new[] { "Use existing repository", "Cancel" }));

            if (useExisting == "Cancel")
            {
                AnsiConsole.MarkupLine("[yellow]Cancelled.[/]");
                return null;
            }

            AnsiConsole.MarkupLine($"[green]✅ Using existing repository:[/] {remoteUrl}");
        }
        else
        {
            // Create repository
            AnsiConsole.MarkupLine($"🔨 Creating repository '[cyan]{repoName}[/]'...");
            var createJson = await RunCommandCapture("az",
                $"repos create --name \"{repoName}\" --organization {orgUrl} --project \"{selectedProject}\" --output json");

            if (createJson == null)
            {
                AnsiConsole.MarkupLine("[red]❌ Failed to create repository.[/]");
                return null;
            }

            try
            {
                using var doc = JsonDocument.Parse(createJson);
                if (doc.RootElement.TryGetProperty("remoteUrl", out var urlProp))
                {
                    remoteUrl = urlProp.GetString();
                }
            }
            catch { }

            if (string.IsNullOrWhiteSpace(remoteUrl))
            {
                AnsiConsole.MarkupLine("[red]❌ Repository created but could not get remote URL.[/]");
                return null;
            }

            AnsiConsole.MarkupLine($"[green]✅ Repository created:[/] {remoteUrl}");
        }

        // 8. Add git remote
        var existingOrigin = await RunCommandCapture("git", "remote get-url origin");
        string finalRemoteName;

        if (existingOrigin != null)
        {
            AnsiConsole.MarkupLine($"\n[yellow]⚠️  Remote 'origin' already exists:[/] {existingOrigin.Trim()}");
            finalRemoteName = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title("Select [green]remote name[/] for Azure DevOps:")
                    .HighlightStyle(new Style(Color.Cyan1))
                    .AddChoices(new[] { "azure", "origin (replace)", "custom" }));

            if (finalRemoteName == "origin (replace)")
            {
                await ExecuteGitCommand($"remote remove origin");
                finalRemoteName = "origin";
            }
            else if (finalRemoteName == "custom")
            {
                finalRemoteName = AnsiConsole.Prompt(
                    new TextPrompt<string>("Enter remote name:")
                        .DefaultValue("azure")
                        .PromptStyle(new Style(Color.Cyan1)));
            }
        }
        else
        {
            finalRemoteName = "origin";
        }

        if (!await ExecuteGitCommand($"remote add {finalRemoteName} {remoteUrl}"))
        {
            AnsiConsole.MarkupLine($"[red]❌ Failed to add remote '{finalRemoteName}'.[/]");
            return null;
        }

        AnsiConsole.MarkupLine($"\n[green]✅ Remote '{finalRemoteName}' configured:[/] {remoteUrl}");

        return finalRemoteName;
    }

    private static async Task<List<string>> FetchAzureOrganizations()
    {
        var organizations = new List<string>();

        // Get user profile to obtain memberId
        AnsiConsole.MarkupLine("📋 Fetching organizations...");
        var profileJson = await RunCommandCapture("az",
            "rest --method get --resource 499b84ac-1321-427f-aa17-267ca6975798 --url https://app.vssps.visualstudio.com/_apis/profile/profiles/me?api-version=7.0");

        string? memberId = null;
        if (profileJson != null)
        {
            try
            {
                using var doc = JsonDocument.Parse(profileJson);
                if (doc.RootElement.TryGetProperty("id", out var idProp))
                {
                    memberId = idProp.GetString();
                }
            }
            catch { }
        }

        if (memberId == null)
        {
            return organizations;
        }

        // List organizations for this member
        var orgsJson = await RunCommandCapture("az",
            $"rest --method get --resource 499b84ac-1321-427f-aa17-267ca6975798 --url \"https://app.vssps.visualstudio.com/_apis/accounts?memberId={memberId}&api-version=7.0\"");

        if (orgsJson == null)
        {
            return organizations;
        }

        try
        {
            using var doc = JsonDocument.Parse(orgsJson);
            JsonElement items;
            if (doc.RootElement.ValueKind == JsonValueKind.Array)
            {
                items = doc.RootElement;
            }
            else if (doc.RootElement.TryGetProperty("value", out var valueProp))
            {
                items = valueProp;
            }
            else
            {
                return organizations;
            }

            foreach (var org in items.EnumerateArray())
            {
                var name = org.TryGetProperty("accountName", out var nameProp)
                    ? nameProp.GetString() : null;
                if (name != null)
                {
                    organizations.Add(name);
                }
            }
        }
        catch { }

        return organizations;
    }

    private static async Task<List<string>> FetchAzureProjects(string orgUrl)
    {
        var projects = new List<string>();
        var projectsJson = await RunCommandCapture("az", $"devops project list --organization {orgUrl} --output json");

        if (projectsJson == null)
        {
            return projects;
        }

        try
        {
            using var doc = JsonDocument.Parse(projectsJson);
            JsonElement items;
            if (doc.RootElement.ValueKind == JsonValueKind.Array)
            {
                items = doc.RootElement;
            }
            else if (doc.RootElement.TryGetProperty("value", out var valueProp))
            {
                items = valueProp;
            }
            else
            {
                return projects;
            }

            foreach (var project in items.EnumerateArray())
            {
                var name = project.TryGetProperty("name", out var nameProp) ? nameProp.GetString() : null;
                if (name != null)
                {
                    projects.Add(name);
                }
            }
        }
        catch { }

        return projects;
    }

    private static async Task<string?> RunCommandCapture(string command, string arguments)
    {
        try
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = command,
                    Arguments = arguments,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();
            var output = await process.StandardOutput.ReadToEndAsync();
            await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();

            return process.ExitCode == 0 ? output : null;
        }
        catch
        {
            return null;
        }
    }

    private static async Task<bool> RunCommandPassthrough(string command, string arguments)
    {
        try
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = command,
                    Arguments = arguments,
                    UseShellExecute = false,
                    CreateNoWindow = false
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
}

// JSON models for DeepSeek API
class DeepSeekResponse
{
    [JsonPropertyName("choices")]
    public Choice[]? Choices { get; set; }
}

class Choice
{
    [JsonPropertyName("message")]
    public Message? Message { get; set; }
}

class Message
{
    [JsonPropertyName("content")]
    public string? Content { get; set; }

    [JsonPropertyName("reasoning_content")]
    public string? ReasoningContent { get; set; }
}
