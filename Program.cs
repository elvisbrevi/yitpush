using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Spectre.Console;
using TextCopy;

namespace YitPush;

class Program
{
    private const string DeepSeekApiUrl = "https://api.deepseek.com/v1/chat/completions";
    private const string DeepSeekModel = "deepseek-reasoner";
    private const string BackOption = "← Back";

    static async Task<int> Main(string[] args)
    {
        try
        {
            Console.OutputEncoding = Encoding.UTF8;
            Console.InputEncoding = Encoding.UTF8;
        }
        catch { }

        Console.WriteLine("🚀 YitPush - AI-Powered Git Commit Tool\n");

        if (args.Length == 0 || args[0] is "--help" or "-h" or "-help" or "help")
        {
            ShowHelp();
            return 0;
        }

        switch (args[0])
        {
            case "commit":
                return await CommitCommand(args.Skip(1).ToArray());
            case "checkout":
                return await CheckoutBranch();
            case "pr":
                return await PrCommand(args.Skip(1).ToArray());
            case "azure-devops":
                return await AzureDevOpsCommand(args.Skip(1).ToArray());
            default:
                AnsiConsole.MarkupLine($"[red]❌ Unknown command:[/] {Markup.Escape(args[0])}\n");
                ShowHelp();
                return 1;
        }
    }

    // ─── Commands ────────────────────────────────────────────────────────────

    private static async Task<int> CommitCommand(string[] args)
    {
        bool requireConfirmation = args.Contains("--confirm");
        bool detailed = args.Contains("--detailed");
        bool save = args.Contains("--save");
        string language = ParseLanguage(args);

        try
        {
            if (!await IsGitRepository())
            {
                AnsiConsole.MarkupLine("[red]❌ Error: Not a git repository.[/]");
                Console.WriteLine("\nPlease run this command from within a git repository.");
                return 1;
            }

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

                try
                {
                    ClipboardService.SetText(commitMessage);
                    AnsiConsole.MarkupLine("[green]✅ Copied to clipboard[/]");
                }
                catch (Exception ex)
                {
                    AnsiConsole.MarkupLine($"[yellow]⚠️  Could not copy to clipboard: {ex.Message}[/]");
                }
                Console.WriteLine();

                if (save)
                {
                    var fileName = $"commit-message-{DateTime.Now:yyyyMMdd-HHmmss}.md";
                    await File.WriteAllTextAsync(fileName, commitMessage);
                    AnsiConsole.MarkupLine($"[green]✅ Commit message saved to:[/] {fileName}\n");
                }

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

            Console.WriteLine("\n⚙️  Executing git commands...");

            if (hasChanges)
            {
                Console.WriteLine("   git add .");
                if (!await ExecuteGitCommand("add ."))
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("❌ Error: git add failed.");
                    Console.ResetColor();
                    return 1;
                }

                Console.WriteLine("   git commit");
                if (!await ExecuteGitCommand($"commit -m \"{commitMessage}\""))
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("❌ Error: git commit failed.");
                    Console.ResetColor();
                    return 1;
                }
            }

            if (!await ExecuteGitPush(requireConfirmation))
                return 1;

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine(hasChanges
                ? "\n✅ Successfully committed and pushed changes!"
                : "\n✅ Successfully pushed changes!");
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

    private static async Task<int> PrCommand(string[] args)
    {
        bool detailed = args.Contains("--detailed");
        bool save = args.Contains("--save");
        string language = ParseLanguage(args);

        if (!await IsGitRepository())
        {
            AnsiConsole.MarkupLine("[red]❌ Error: Not a git repository.[/]");
            Console.WriteLine("\nPlease run this command from within a git repository.");
            return 1;
        }

        return await GeneratePrDescription(detailed, language, save);
    }

    private static async Task<int> AzureDevOpsCommand(string[] args)
    {
        if (args.Length < 2)
        {
            ShowAzureDevOpsHelp();
            return 1;
        }

        var resource = args[0];
        var action = args[1];

        if (resource == "repo" && action == "new")
        {
            var remote = await CreateAzureDevOpsRepo();
            return remote != null ? 0 : 1;
        }
        else if (resource == "repo" && action == "checkout")
        {
            return await CheckoutAzureDevOpsRepo();
        }
        else if (resource == "variable-group" && action == "list")
        {
            return await ListAzureVariableGroups();
        }
        else
        {
            AnsiConsole.MarkupLine($"[red]❌ Unknown command:[/] azure-devops {Markup.Escape(resource)} {Markup.Escape(action)}\n");
            ShowAzureDevOpsHelp();
            return 1;
        }
    }

    // ─── Help ─────────────────────────────────────────────────────────────────

    private static void ShowHelp()
    {
        AnsiConsole.MarkupLine("[bold]Usage:[/] yitpush [bold]<command>[/] [[options]]\n");

        var commandsTable = new Table()
            .Border(TableBorder.Rounded)
            .BorderColor(Color.Cyan1)
            .Title("[bold cyan]Commands[/]")
            .AddColumn(new TableColumn("[bold]Command[/]").NoWrap())
            .AddColumn(new TableColumn("[bold]Description[/]"));

        commandsTable.AddRow("commit", "Stage, commit and push changes with an AI-generated message");
        commandsTable.AddRow("checkout", "Interactive branch checkout");
        commandsTable.AddRow("pr", "Generate a pull request description between two branches");
        commandsTable.AddRow("azure-devops", "Manage Azure DevOps resources (repos, variable groups)");

        AnsiConsole.Write(commandsTable);

        AnsiConsole.MarkupLine("\n[bold cyan]commit[/] options:");
        var commitTable = new Table()
            .Border(TableBorder.Rounded)
            .BorderColor(Color.Grey)
            .AddColumn(new TableColumn("[bold]Flag[/]").NoWrap())
            .AddColumn(new TableColumn("[bold]Description[/]"));

        commitTable.AddRow("--confirm", "Ask for confirmation before committing");
        commitTable.AddRow("--detailed", "Generate detailed commit with title + body");
        commitTable.AddRow("--language <lang>", "Output language (e.g., english, spanish, french)");
        commitTable.AddRow("--save", "Save commit message to a markdown file");

        AnsiConsole.Write(commitTable);

        AnsiConsole.MarkupLine("\n[bold cyan]pr[/] options:");
        var prTable = new Table()
            .Border(TableBorder.Rounded)
            .BorderColor(Color.Grey)
            .AddColumn(new TableColumn("[bold]Flag[/]").NoWrap())
            .AddColumn(new TableColumn("[bold]Description[/]"));

        prTable.AddRow("--detailed", "Generate detailed PR description");
        prTable.AddRow("--language <lang>", "Output language");
        prTable.AddRow("--save", "Save PR description to a markdown file");

        AnsiConsole.Write(prTable);

        AnsiConsole.MarkupLine("\n[bold cyan]azure-devops[/] subcommands:");
        var azTable = new Table()
            .Border(TableBorder.Rounded)
            .BorderColor(Color.Grey)
            .AddColumn(new TableColumn("[bold]Subcommand[/]").NoWrap())
            .AddColumn(new TableColumn("[bold]Description[/]"));

        azTable.AddRow("repo new", "Create a new repository interactively");
        azTable.AddRow("repo checkout", "Clone a repository interactively");
        azTable.AddRow("variable-group list", "List and inspect variable groups");

        AnsiConsole.Write(azTable);

        AnsiConsole.MarkupLine("\n[bold]Examples:[/]");
        AnsiConsole.MarkupLine("  yitpush commit                              [dim]# Auto commit and push[/]");
        AnsiConsole.MarkupLine("  yitpush commit --confirm                    [dim]# Review before committing[/]");
        AnsiConsole.MarkupLine("  yitpush commit --detailed --lang es         [dim]# Detailed commit, in Spanish[/]");
        AnsiConsole.MarkupLine("  yitpush checkout                            [dim]# Switch branch interactively[/]");
        AnsiConsole.MarkupLine("  yitpush pr                                  [dim]# Generate PR description[/]");
        AnsiConsole.MarkupLine("  yitpush pr --detailed --save                [dim]# Detailed PR, save to file[/]");
        AnsiConsole.MarkupLine("  yitpush azure-devops repo new               [dim]# Create Azure DevOps repo[/]");
        AnsiConsole.MarkupLine("  yitpush azure-devops repo checkout          [dim]# Clone Azure DevOps repo[/]");
        AnsiConsole.MarkupLine("  yitpush azure-devops variable-group list    [dim]# Browse variable groups[/]");
        Console.WriteLine();
    }

    private static void ShowAzureDevOpsHelp()
    {
        AnsiConsole.MarkupLine("[bold]Usage:[/] yitpush azure-devops [bold]<subcommand>[/]\n");

        var table = new Table()
            .Border(TableBorder.Rounded)
            .BorderColor(Color.Cyan1)
            .Title("[bold cyan]azure-devops subcommands[/]")
            .AddColumn(new TableColumn("[bold]Subcommand[/]").NoWrap())
            .AddColumn(new TableColumn("[bold]Description[/]"));

        table.AddRow("repo new", "Create a new repository interactively");
        table.AddRow("repo checkout", "Clone a repository interactively");
        table.AddRow("variable-group list", "List and inspect variable groups");

        AnsiConsole.Write(table);

        AnsiConsole.MarkupLine("\n[bold]Examples:[/]");
        AnsiConsole.MarkupLine("  yitpush azure-devops repo new              [dim]# Create Azure DevOps repo[/]");
        AnsiConsole.MarkupLine("  yitpush azure-devops repo checkout         [dim]# Clone Azure DevOps repo[/]");
        AnsiConsole.MarkupLine("  yitpush azure-devops variable-group list   [dim]# List variable groups[/]");
        Console.WriteLine();
    }

    // ─── Helpers ──────────────────────────────────────────────────────────────

    private static string ParseLanguage(string[] args)
    {
        for (int i = 0; i < args.Length; i++)
        {
            if ((args[i] == "--language" || args[i] == "--lang") && i + 1 < args.Length && !args[i + 1].StartsWith("--"))
                return args[i + 1];
            if (args[i].StartsWith("--language=") || args[i].StartsWith("--lang="))
                return args[i].Split('=', 2)[1];
        }
        return "english";
    }

    // ─── Git helpers ──────────────────────────────────────────────────────────

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
        const int deepseekMaxContextTokens = 131072;
        const int maxCompletionTokens = 8000;
        const int reservedTokens = maxCompletionTokens + 8000; // extra buffer for system prompt + formatting
        const int maxPromptTokens = deepseekMaxContextTokens - reservedTokens;
        const int averageCharsPerToken = 3; // conservative estimate for code diffs
        const int maxPromptChars = maxPromptTokens * averageCharsPerToken;

        diff = TruncateDiff(diff, maxPromptChars);

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

        return await CallDeepSeekApi(apiKey, prompt);
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

    private static async Task<string> CallDeepSeekApi(string apiKey, string prompt)
    {
        const int maxRetries = 3;
        const int baseDelayMs = 1000;

        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                using var httpClient = new HttpClient
                {
                    Timeout = TimeSpan.FromSeconds(120)
                };
                httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");

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

    // ─── PR Description ───────────────────────────────────────────────────────

    private static async Task<string> GeneratePrDescriptionContent(string apiKey, string diff, bool detailed, string language)
    {
        const int deepseekMaxContextTokens = 131072;
        const int maxCompletionTokens = 8000;
        const int reservedTokens = maxCompletionTokens + 8000; // extra buffer for system prompt + formatting
        const int maxPromptTokens = deepseekMaxContextTokens - reservedTokens;
        const int averageCharsPerToken = 3; // conservative estimate for code diffs
        const int maxPromptChars = maxPromptTokens * averageCharsPerToken;

        diff = TruncateDiff(diff, maxPromptChars);

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

        return await CallDeepSeekApi(apiKey, prompt);
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

        string fromBranch;
        string toBranch;

        while (true)
        {
            // Select source branch (from - the branch with changes)
            var sourceChoices = new List<string>(displayList) { BackOption };
            var fromDisplay = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title("📋 Select [green]source[/] branch (branch with changes):\n[dim](Select ← Back to return to previous step)[/]")
                    .PageSize(15)
                    .HighlightStyle(new Style(Color.Cyan1))
                    .AddChoices(sourceChoices));

            if (fromDisplay == BackOption) return 0;

            fromBranch = displayMap[fromDisplay];
            AnsiConsole.MarkupLine($"\n[green]✅ Source branch:[/] {fromBranch}");

            // Select target branch (to - the branch to merge into)
            var targetDisplayList = displayList.Where(d => displayMap[d] != fromBranch).ToList();
            var targetChoices = new List<string>(targetDisplayList) { BackOption };
            var toDisplay = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title("📋 Select [green]target[/] branch (branch to merge into):\n[dim](Select ← Back to return to previous step)[/]")
                    .PageSize(15)
                    .HighlightStyle(new Style(Color.Cyan1))
                    .AddChoices(targetChoices));

            if (toDisplay == BackOption) continue;

            toBranch = displayMap[toDisplay];
            AnsiConsole.MarkupLine($"\n[green]✅ Target branch:[/] {toBranch}");
            break;
        }

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

        // Copy to clipboard
        try
        {
            ClipboardService.SetText(description);
            AnsiConsole.MarkupLine("\n[green]✅ Copied to clipboard[/]");
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"\n[yellow]⚠️  Could not copy to clipboard: {ex.Message}[/]");
        }

        if (save)
        {
            var fileName = $"pr-description-{fromBranch.Replace("/", "-")}-to-{toBranch.Replace("/", "-")}.md";
            await File.WriteAllTextAsync(fileName, description);
            AnsiConsole.MarkupLine($"\n[green]✅ PR description saved to:[/] {fileName}");
        }

        return 0;
    }

    // ─── Branch checkout ──────────────────────────────────────────────────────

    private static async Task<int> CheckoutBranch()
    {
        if (!await IsGitRepository())
        {
            AnsiConsole.MarkupLine("[red]❌ Error: Not a git repository.[/]");
            Console.WriteLine("\nPlease run this command from within a git repository.");
            return 1;
        }

        AnsiConsole.MarkupLine("[bold blue]🔀 Interactive Branch Checkout[/]\n");

        // Fetch latest remote branches
        AnsiConsole.MarkupLine("🔄 Fetching remote branches...");
        if (!await ExecuteGitCommand("fetch --all --prune"))
        {
            AnsiConsole.MarkupLine("[yellow]⚠️  Could not fetch remote branches. Continuing with known branches...[/]\n");
        }

        // Get all branches
        var branches = await GetGitBranches();
        var currentBranch = await GetCurrentBranch();

        // Filter out current branch
        if (!string.IsNullOrEmpty(currentBranch))
        {
            branches = branches.Where(b => b.Name != currentBranch).ToList();
            AnsiConsole.MarkupLine($"Current branch: [cyan]{currentBranch}[/]\n");
        }

        if (branches.Count == 0)
        {
            AnsiConsole.MarkupLine("[yellow]⚠️  No other branches found.[/]");
            return 0;
        }

        // Build display strings
        var maxNameLen = branches.Max(b => b.Name.Length);
        var displayMap = new Dictionary<string, (string Name, string Type)>();
        var displayList = new List<string>();

        foreach (var b in branches)
        {
            var display = $"{b.Name.PadRight(maxNameLen + 2)} {b.Type.PadRight(8)} {b.Date}";
            displayMap[display] = (b.Name, b.Type);
            displayList.Add(display);
        }

        var choices = new List<string>(displayList) { BackOption };
        var selected = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("📋 Select branch to checkout:\n[dim](Select ← Back to cancel)[/]")
                .PageSize(15)
                .HighlightStyle(new Style(Color.Cyan1))
                .AddChoices(choices));

        if (selected == BackOption) return 0;

        var (branchName, branchType) = displayMap[selected];

        // Checkout the branch
        string checkoutArgs;
        if (branchType == "remote")
        {
            // Remote branch: extract the name after the remote prefix (e.g., "origin/feature-x" -> "feature-x")
            var slashIndex = branchName.IndexOf('/');
            var localName = slashIndex >= 0 ? branchName.Substring(slashIndex + 1) : branchName;
            checkoutArgs = $"checkout -t {branchName}";
            AnsiConsole.MarkupLine($"\n⚙️  Checking out remote branch [cyan]{branchName}[/] as [cyan]{localName}[/]...");
        }
        else
        {
            checkoutArgs = $"checkout {branchName}";
            AnsiConsole.MarkupLine($"\n⚙️  Checking out [cyan]{branchName}[/]...");
        }

        if (await ExecuteGitCommand(checkoutArgs))
        {
            AnsiConsole.MarkupLine($"[green]✅ Switched to branch '{branchName}'[/]");
            AnsiConsole.MarkupLine("🔄 Pulling latest changes...");
            if (!await ExecuteGitCommand("pull"))
            {
                AnsiConsole.MarkupLine("[yellow]⚠️  Could not pull latest changes.[/]");
            }
            return 0;
        }
        else
        {
            // If tracking checkout failed (branch already exists locally), try plain checkout
            if (branchType == "remote")
            {
                var slashIndex = branchName.IndexOf('/');
                var localName = slashIndex >= 0 ? branchName.Substring(slashIndex + 1) : branchName;
                AnsiConsole.MarkupLine($"[yellow]⚠️  Tracking branch may already exist. Trying local checkout...[/]");
                if (await ExecuteGitCommand($"checkout {localName}"))
                {
                    AnsiConsole.MarkupLine($"[green]✅ Switched to branch '{localName}'[/]");
                    AnsiConsole.MarkupLine("🔄 Pulling latest changes...");
                    if (!await ExecuteGitCommand("pull"))
                    {
                        AnsiConsole.MarkupLine("[yellow]⚠️  Could not pull latest changes.[/]");
                    }
                    return 0;
                }
            }
            AnsiConsole.MarkupLine("[red]❌ Failed to checkout branch.[/]");
            return 1;
        }
    }

    // ─── Azure DevOps ─────────────────────────────────────────────────────────

    private static async Task<(string OrgUrl, string Project)?> EnsureAzureDevOpsSetup()
    {
        // 1. Check if az CLI is installed
        var azCheck = await RunAzCapture("--version");
        if (azCheck == null)
        {
            AnsiConsole.MarkupLine("[red]❌ Azure CLI (az) is not installed.[/]");
            AnsiConsole.MarkupLine("\nInstall it from: https://docs.microsoft.com/en-us/cli/azure/install-azure-cli");
            return null;
        }

        // 2. Check if azure-devops extension is installed
        AnsiConsole.MarkupLine("🔍 Checking Azure DevOps extension...");
        var extCheck = await RunAzCapture("extension show --name azure-devops --output json");
        if (extCheck == null)
        {
            AnsiConsole.MarkupLine("📦 Installing Azure DevOps extension...");
            var installResult = await RunAzPassthrough("extension add --name azure-devops");
            if (!installResult)
            {
                AnsiConsole.MarkupLine("[red]❌ Failed to install Azure DevOps extension.[/]");
                return null;
            }
        }
        AnsiConsole.MarkupLine("[green]✅ Azure DevOps extension available.[/]\n");

        // 3. Check if logged in
        var accountJson = await RunAzCapture("account show --output json");
        if (accountJson == null)
        {
            AnsiConsole.MarkupLine("🔐 Not logged into Azure. Starting interactive login...\n");
            var loginResult = await RunAzPassthrough("login");
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
            AnsiConsole.MarkupLine("[yellow]⚠️  Could not automatically detect Azure DevOps organizations.[/]");
            AnsiConsole.MarkupLine("[dim]This can happen if your Azure account is not linked to Azure DevOps via the API.[/]\n");

            var manualChoice = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title("How would you like to specify the organization?")
                    .HighlightStyle(new Style(Color.Cyan1))
                    .AddChoices("Enter organization name manually", "Cancel"));

            if (manualChoice == "Cancel") return null;

            var orgName = AnsiConsole.Prompt(
                new TextPrompt<string>("Enter organization name [dim](from https://dev.azure.com/[bold]organization[/])[/]:")
                    .PromptStyle(new Style(Color.Cyan1)));

            if (string.IsNullOrWhiteSpace(orgName)) return null;

            organizations.Add(orgName.Trim());
        }

        organizations.Sort(StringComparer.OrdinalIgnoreCase);

        while (true)
        {
            var orgChoices = new List<string>(organizations) { BackOption };
            var selectedOrg = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title("📋 Select [green]organization[/]:\n[dim](Select ← Back to return to previous step)[/]")
                    .PageSize(10)
                    .HighlightStyle(new Style(Color.Cyan1))
                    .AddChoices(orgChoices));

            if (selectedOrg == BackOption) return null;

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
            var projectChoices = new List<string>(projects) { BackOption };
            var selectedProject = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title("📋 Select [green]project[/]:\n[dim](Select ← Back to return to previous step)[/]")
                    .PageSize(10)
                    .HighlightStyle(new Style(Color.Cyan1))
                    .AddChoices(projectChoices));

            if (selectedProject == BackOption) continue;

            AnsiConsole.MarkupLine($"\n[green]✅ Project:[/] {selectedProject}");

            return (orgUrl, selectedProject);
        }
    }

    private static async Task<string?> CreateAzureDevOpsRepo()
    {
        AnsiConsole.MarkupLine("[bold blue]☁️  Azure DevOps - Create New Repository[/]\n");

        var setup = await EnsureAzureDevOpsSetup();
        if (setup == null) return null;

        var (orgUrl, selectedProject) = setup.Value;

        while (true)
        {
            // 6. Repository name (suggest current directory name as default)
            var currentDirName = new DirectoryInfo(Directory.GetCurrentDirectory()).Name;
            var repoName = AnsiConsole.Prompt(
                new TextPrompt<string>("📝 Repository name:")
                    .DefaultValue(currentDirName)
                    .PromptStyle(new Style(Color.Cyan1)));

            // 7. Check if repo already exists
            AnsiConsole.MarkupLine($"\n🔍 Checking if repository '[cyan]{repoName}[/]' already exists...");
            var existingRepoJson = await RunAzCapture(
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
                var createJson = await RunAzCapture(
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
                        .Title("Select [green]remote name[/] for Azure DevOps:\n[dim](Select ← Back to return to previous step)[/]")
                        .HighlightStyle(new Style(Color.Cyan1))
                        .AddChoices(new[] { "azure", "origin (replace)", "custom", BackOption }));

                if (finalRemoteName == BackOption) continue;

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
    }

    private static async Task<int> CheckoutAzureDevOpsRepo()
    {
        AnsiConsole.MarkupLine("[bold blue]☁️  Azure DevOps - Clone Repository[/]\n");

        var setup = await EnsureAzureDevOpsSetup();
        if (setup == null) return 1;

        var (orgUrl, selectedProject) = setup.Value;

        AnsiConsole.MarkupLine("\n📋 Fetching repositories...");
        var repos = await FetchAzureRepos(orgUrl, selectedProject);

        if (repos.Count == 0)
        {
            AnsiConsole.MarkupLine("[red]❌ No repositories found in this project.[/]");
            return 1;
        }

        repos.Sort((a, b) => StringComparer.OrdinalIgnoreCase.Compare(a.Name, b.Name));

        var repoChoices = new List<string>(repos.Select(r => r.Name)) { BackOption };
        var selectedRepoName = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("📋 Select [green]repository[/] to clone:\n[dim](Select ← Back to return to previous step)[/]")
                .PageSize(15)
                .HighlightStyle(new Style(Color.Cyan1))
                .AddChoices(repoChoices));

        if (selectedRepoName == BackOption) return 0;

        var selectedRepo = repos.First(r => r.Name == selectedRepoName);
        AnsiConsole.MarkupLine($"\n[green]✅ Repository:[/] {selectedRepo.Name}");

        var defaultDir = Path.Combine(Directory.GetCurrentDirectory(), selectedRepo.Name);
        var targetDir = AnsiConsole.Prompt(
            new TextPrompt<string>("📁 Clone into directory:")
                .DefaultValue(defaultDir)
                .PromptStyle(new Style(Color.Cyan1)));

        AnsiConsole.MarkupLine($"\n🔄 Cloning [cyan]{selectedRepo.Name}[/]...");
        var success = await RunCommandPassthrough("git", $"clone \"{selectedRepo.RemoteUrl}\" \"{targetDir}\"");

        if (!success)
        {
            AnsiConsole.MarkupLine("[red]❌ Failed to clone repository.[/]");
            return 1;
        }

        AnsiConsole.MarkupLine($"\n[green]✅ Repository cloned successfully into:[/] {targetDir}");
        return 0;
    }

    private static async Task<int> ListAzureVariableGroups()
    {
        AnsiConsole.MarkupLine("[bold blue]☁️  Azure DevOps - Variable Groups[/]\n");

        var setup = await EnsureAzureDevOpsSetup();
        if (setup == null) return 1;

        var (orgUrl, project) = setup.Value;

        AnsiConsole.MarkupLine("\n📋 Fetching variable groups...");
        var json = await RunAzCapture(
            $"pipelines variable-group list --organization {orgUrl} --project \"{project}\" --output json");

        if (json == null)
        {
            AnsiConsole.MarkupLine("[red]❌ Failed to fetch variable groups.[/]");
            return 1;
        }

        try
        {
            using var doc = JsonDocument.Parse(json);
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
                AnsiConsole.MarkupLine("[yellow]⚠️  No variable groups found.[/]");
                return 0;
            }

            if (items.GetArrayLength() == 0)
            {
                AnsiConsole.MarkupLine("[yellow]⚠️  No variable groups found in this project.[/]");
                return 0;
            }

            // Parse groups into a list for the selectable prompt
            var groups = new List<(string Id, string Name, string Description, int VarCount, JsonElement Variables)>();
            foreach (var group in items.EnumerateArray())
            {
                var id = group.TryGetProperty("id", out var idProp) ? idProp.ToString() : "-";
                var name = group.TryGetProperty("name", out var nameProp) ? nameProp.GetString() ?? "-" : "-";
                var description = group.TryGetProperty("description", out var descProp) ? descProp.GetString() ?? "" : "";
                var variableCount = 0;
                JsonElement variablesElement = default;
                if (group.TryGetProperty("variables", out var varsProp) && varsProp.ValueKind == JsonValueKind.Object)
                {
                    variableCount = varsProp.EnumerateObject().Count();
                    variablesElement = varsProp.Clone();
                }
                groups.Add((id, name, description, variableCount, variablesElement));
            }

            // Build columnar display strings
            var maxIdLen = groups.Max(g => g.Id.Length);
            var maxNameLen = groups.Max(g => g.Name.Length);
            var maxDescLen = Math.Min(groups.Max(g => g.Description.Length), 30);

            var displayMap = new Dictionary<string, int>(); // display -> index
            var displayList = new List<string>();

            for (int i = 0; i < groups.Count; i++)
            {
                var g = groups[i];
                var desc = g.Description.Length > 30 ? g.Description.Substring(0, 27) + "..." : g.Description;
                var display = $"{g.Id.PadRight(maxIdLen + 2)} {g.Name.PadRight(maxNameLen + 2)} {desc.PadRight(maxDescLen + 2)} Vars: {g.VarCount}";
                displayMap[display] = i;
                displayList.Add(display);
            }

            while (true)
            {
                var choices = new List<string>(displayList) { BackOption };
                var selected = AnsiConsole.Prompt(
                    new SelectionPrompt<string>()
                        .Title("📋 Select a [green]variable group[/] to view its variables:\n[dim](Select ← Back to return)[/]")
                        .PageSize(15)
                        .HighlightStyle(new Style(Color.Cyan1))
                        .AddChoices(choices));

                if (selected == BackOption) return 0;

                var idx = displayMap[selected];
                var selectedGroup = groups[idx];

                AnsiConsole.MarkupLine($"\n[bold cyan]Variables in '{Markup.Escape(selectedGroup.Name)}':[/]\n");

                if (selectedGroup.Variables.ValueKind == JsonValueKind.Object)
                {
                    var varTable = new Table()
                        .Border(TableBorder.Rounded)
                        .BorderColor(Color.Cyan1)
                        .AddColumn(new TableColumn("[bold]Name[/]"))
                        .AddColumn(new TableColumn("[bold]Value[/]"));

                    foreach (var variable in selectedGroup.Variables.EnumerateObject())
                    {
                        var varName = variable.Name;
                        var isSecret = variable.Value.TryGetProperty("isSecret", out var secretProp)
                            && secretProp.ValueKind == JsonValueKind.True;
                        var varValue = isSecret
                            ? "******"
                            : variable.Value.TryGetProperty("value", out var valProp) && valProp.ValueKind == JsonValueKind.String
                                ? valProp.GetString() ?? ""
                                : "";

                        varTable.AddRow(Markup.Escape(varName), Markup.Escape(varValue));
                    }

                    AnsiConsole.Write(varTable);
                }
                else
                {
                    AnsiConsole.MarkupLine("[yellow]No variables found in this group.[/]");
                }

                AnsiConsole.MarkupLine("\n[dim]Press any key to return to the list...[/]");
                Console.ReadKey(true);
                AnsiConsole.WriteLine();
            }
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]❌ Error parsing variable groups: {ex.Message}[/]");
            return 1;
        }
    }

    private static bool IsAzureAuthError(string? error)
    {
        if (string.IsNullOrEmpty(error)) return false;
        return error.Contains("AADSTS50078") // MFA expired
            || error.Contains("AADSTS700082") // Token expired
            || error.Contains("AADSTS50076") // MFA required
            || error.Contains("AADSTS70043") // Bad token
            || error.Contains("AADSTS50173") // Credential expired
            || error.Contains("AADSTS700024") // Client assertion expired
            || error.Contains("AADSTS65001") // Consent required
            || error.Contains("InvalidAuthenticationToken")
            || error.Contains("Authentication failed");
    }

    private static async Task<bool> HandleAzureReLogin()
    {
        AnsiConsole.MarkupLine("[yellow]⚠️  Your Azure session has expired or requires re-authentication.[/]");
        var reloginChoice = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("Would you like to [green]re-login[/] to Azure?")
                .HighlightStyle(new Style(Color.Cyan1))
                .AddChoices("Yes, re-login", "Cancel"));

        if (reloginChoice == "Cancel") return false;

        AnsiConsole.MarkupLine("\n🔐 Starting Azure re-authentication...\n");
        var logoutResult = await RunAzPassthrough("logout");
        if (!logoutResult)
        {
            AnsiConsole.MarkupLine("[dim]Note: logout returned an error (may already be logged out).[/]");
        }

        var loginResult = await RunAzPassthrough("login");
        if (!loginResult)
        {
            AnsiConsole.MarkupLine("[red]❌ Azure login failed.[/]");
            return false;
        }

        AnsiConsole.MarkupLine("\n[green]✅ Successfully re-authenticated with Azure.[/]\n");
        return true;
    }

    private static async Task<List<string>> FetchAzureOrganizations()
    {
        var organizations = new List<string>();
        const string azDevOpsResource = "499b84ac-1321-427f-aa17-267ca6975798";

        // Get user profile to obtain memberId
        AnsiConsole.MarkupLine("📋 Fetching organizations...");
        var (profileJson, profileError) = await RunAzCaptureWithError(
            $"rest --method get --resource {azDevOpsResource} --url https://app.vssps.visualstudio.com/_apis/profile/profiles/me?api-version=7.0");

        if (profileJson == null && IsAzureAuthError(profileError))
        {
            if (!await HandleAzureReLogin()) return organizations;

            (profileJson, profileError) = await RunAzCaptureWithError(
                $"rest --method get --resource {azDevOpsResource} --url https://app.vssps.visualstudio.com/_apis/profile/profiles/me?api-version=7.0");
        }

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
            if (profileError != null)
            {
                AnsiConsole.MarkupLine($"[red]❌ Failed to fetch Azure DevOps profile:[/] [dim]{Markup.Escape(profileError.Trim())}[/]");
            }
            return organizations;
        }

        // List organizations for this member
        var (orgsJson, orgsError) = await RunAzCaptureWithError(
            $"rest --method get --resource {azDevOpsResource} --url \"https://app.vssps.visualstudio.com/_apis/accounts?memberId={memberId}&api-version=7.0\"");

        if (orgsJson == null && IsAzureAuthError(orgsError))
        {
            if (!await HandleAzureReLogin()) return organizations;

            (orgsJson, orgsError) = await RunAzCaptureWithError(
                $"rest --method get --resource {azDevOpsResource} --url \"https://app.vssps.visualstudio.com/_apis/accounts?memberId={memberId}&api-version=7.0\"");
        }

        if (orgsJson == null)
        {
            if (orgsError != null)
            {
                AnsiConsole.MarkupLine($"[red]❌ Failed to fetch organizations:[/] [dim]{Markup.Escape(orgsError.Trim())}[/]");
            }
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
        var projectsJson = await RunAzCapture($"devops project list --organization {orgUrl} --output json");

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

    private static async Task<List<(string Name, string RemoteUrl)>> FetchAzureRepos(string orgUrl, string project)
    {
        var repos = new List<(string Name, string RemoteUrl)>();
        var reposJson = await RunAzCapture(
            $"repos list --organization {orgUrl} --project \"{project}\" --output json");

        if (reposJson == null) return repos;

        try
        {
            using var doc = JsonDocument.Parse(reposJson);
            JsonElement items;
            if (doc.RootElement.ValueKind == JsonValueKind.Array)
                items = doc.RootElement;
            else if (doc.RootElement.TryGetProperty("value", out var valueProp))
                items = valueProp;
            else
                return repos;

            foreach (var repo in items.EnumerateArray())
            {
                var name = repo.TryGetProperty("name", out var nameProp) ? nameProp.GetString() : null;
                var remoteUrl = repo.TryGetProperty("remoteUrl", out var urlProp) ? urlProp.GetString() : null;
                if (name != null && remoteUrl != null)
                    repos.Add((name, remoteUrl));
            }
        }
        catch { }

        return repos;
    }

    // ─── Process helpers ──────────────────────────────────────────────────────

    private static Task<string?> RunAzCapture(string arguments)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return RunCommandCapture("cmd.exe", $"/c az {arguments}");
        return RunCommandCapture("az", arguments);
    }

    private static Task<(string? Output, string? Error)> RunAzCaptureWithError(string arguments)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return RunCommandCaptureWithError("cmd.exe", $"/c az {arguments}");
        return RunCommandCaptureWithError("az", arguments);
    }

    private static Task<bool> RunAzPassthrough(string arguments)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return RunCommandPassthrough("cmd.exe", $"/c az {arguments}");
        return RunCommandPassthrough("az", arguments);
    }

    private static async Task<(string? Output, string? Error)> RunCommandCaptureWithError(string command, string arguments)
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
            var error = await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();

            return process.ExitCode == 0 ? (output, null) : (null, error);
        }
        catch (Exception ex)
        {
            return (null, ex.Message);
        }
    }

    private static async Task<string?> RunCommandCapture(string command, string arguments)
    {
        var (output, _) = await RunCommandCaptureWithError(command, arguments);
        return output;
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
