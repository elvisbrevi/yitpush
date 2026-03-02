using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Spectre.Console;
using TextCopy;

namespace YitPush;

class Program
{
    private const string DeepSeekApiUrl = "https://api.deepseek.com/v1/chat/completions";
    private const string DeepSeekModel = "deepseek-reasoner";
    private const string BackOption = "← Back";
    private const int BackToMenu = 100;

    static async Task<int> Main(string[] args)
    {
        try
        {
            Console.OutputEncoding = Encoding.UTF8;
            Console.InputEncoding = Encoding.UTF8;
        }
        catch { }

        Console.WriteLine();

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
                if (!await ExecuteGitCommitWithMessage(commitMessage))
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
        // If no arguments, show interactive menu
        if (args.Length == 0)
        {
            return await AzureDevOpsInteractiveMenu();
        }

        // Handle single-arg commands
        if (args[0] == "link")
        {
            if (args.Length >= 4)
            {
                var org = args[1];
                var proj = args[2];
                var wiId = args[3];
                var orgUrl = $"https://dev.azure.com/{org}";
                return await AddLinkToRepo(orgUrl, proj, wiId);
            }
            var setup = await EnsureAzureDevOpsSetup();
            if (setup == null) return 1;
            var (oUrl, project) = setup.Value;
            var workItemId = AnsiConsole.Prompt(
                new TextPrompt<string>("Work item ID:"));
            return await AddLinkToRepo(oUrl, project, workItemId);
        }

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
            var result = await CheckoutAzureDevOpsRepo();
            return result == BackToMenu ? 0 : result;
        }
        else if (resource == "variable-group" && action == "list")
        {
            var result = await ListAzureVariableGroups();
            return result == BackToMenu ? 0 : result;
        }
        else if (resource == "hu" && action == "task")
        {
            // Quick mode: yitpush azure-devops hu task <org> <project> <hu-id>
            if (args.Length >= 5)
            {
                var org = args[2];
                var proj = args[3];
                var huId = args[4];
                var orgUrl = $"https://dev.azure.com/{org}";
                return await CreateTasksDirectForHU(orgUrl, proj, huId);
            }
            var result = await ListAzureUserStories();
            return result == BackToMenu ? 0 : result;
        }
        else if (resource == "hu" && action == "list")
        {
            // Quick mode: yitpush azure-devops hu list <org> <project> <hu-id>
            if (args.Length >= 5)
            {
                var org = args[2];
                var proj = args[3];
                var huId = args[4];
                var orgUrl = $"https://dev.azure.com/{org}";
                return await ListTasksForHU(orgUrl, proj, huId);
            }
            // Interactive mode: select HU first, then list tasks
            var result = await ListAzureUserStoriesForTaskList();
            return result == BackToMenu ? 0 : result;
        }
        else
        {
            AnsiConsole.MarkupLine($"[red]❌ Unknown command:[/] azure-devops {Markup.Escape(resource)} {Markup.Escape(action)}\n");
            ShowAzureDevOpsHelp();
            return 1;
        }
    }

    private static async Task<int> AzureDevOpsInteractiveMenu()
    {
        while (true)
        {
            var choice = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title("[bold]Azure DevOps[/]")
                    .PageSize(10)
                    .HighlightStyle(new Style(Color.Cyan1))
                    .AddChoices(
                        "Create new repository",
                        "Clone repository",
                        "Browse variable groups",
                        "Create tasks for User Story",
                        "List tasks of User Story",
                        "Add link to work item",
                        "Exit"
                    ));

            if (choice == "Exit")
                return 0;

            int result = 0;
            if (choice == "Create new repository")
            {
                var remote = await CreateAzureDevOpsRepo();
                result = remote != null ? 0 : 1;
            }
            else if (choice == "Clone repository")
            {
                result = await CheckoutAzureDevOpsRepo();
            }
            else if (choice == "Browse variable groups")
            {
                result = await ListAzureVariableGroups();
            }
            else if (choice == "Create tasks for User Story")
            {
                result = await ListAzureUserStories();
            }
            else if (choice == "List tasks of User Story")
            {
                result = await ListAzureUserStoriesForTaskList();
            }
            else if (choice == "Add link to work item")
            {
                var setup = await EnsureAzureDevOpsSetup();
                if (setup != null)
                {
                    var (oUrl, proj) = setup.Value;
                    var workItemId = AnsiConsole.Prompt(
                        new TextPrompt<string>("Work item ID:"));
                    result = await AddLinkToRepo(oUrl, proj, workItemId);
                }
            }

            // If command returned BackToMenu, continue showing menu
            if (result == BackToMenu)
                continue;
            
            // Otherwise, return the result (0 for success, 1 for error)
            // But in interactive mode, we might want to stay in menu even after success
            // Let's always return to menu unless result indicates error and we want to exit?
            // For now, after any command completion, show menu again.
            // However, if result is 1 (error), maybe we should still show menu?
            // Let's just continue loop.
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
        azTable.AddRow("hu task", "Create tasks for a User Story");
        azTable.AddRow("hu task <org> <proj> <hu-id>", "Create tasks (skip menus)");
        azTable.AddRow("hu list", "List tasks of a User Story");
        azTable.AddRow("hu list <org> <proj> <hu-id>", "List tasks (skip menus)");
        azTable.AddRow("link", "Add link (branch/commit/PR) to work item");
        azTable.AddRow("link <org> <proj> <wi-id>", "Add link (skip menus)");

        AnsiConsole.Write(azTable);

        AnsiConsole.MarkupLine("\n[bold]Examples:[/]");
        AnsiConsole.MarkupLine("  yitpush commit                                  [dim]# Auto commit and push[/]");
        AnsiConsole.MarkupLine("  yitpush commit --confirm                        [dim]# Review before committing[/]");
        AnsiConsole.MarkupLine("  yitpush checkout                                [dim]# Switch branch interactively[/]");
        AnsiConsole.MarkupLine("  yitpush pr                                      [dim]# Generate PR description[/]");
        AnsiConsole.MarkupLine("  yitpush azure-devops hu task                    [dim]# Create tasks interactively[/]");
        AnsiConsole.MarkupLine("  yitpush azure-devops hu task MyOrg MyProj 12345 [dim]# Create tasks (quick)[/]");
        AnsiConsole.MarkupLine("  yitpush azure-devops hu list MyOrg MyProj 12345 [dim]# List tasks of HU[/]");
        AnsiConsole.MarkupLine("  yitpush azure-devops link MyOrg MyProj 12345    [dim]# Add link to work item[/]");
        Console.WriteLine();
    }

    private static void ShowAzureDevOpsHelp()
    {
        AnsiConsole.MarkupLine("[bold]Usage:[/] yitpush azure-devops [bold]<subcommand>[/]");
        AnsiConsole.MarkupLine("[dim]Run without subcommands for interactive menu[/]\n");

        var table = new Table()
            .Border(TableBorder.Rounded)
            .BorderColor(Color.Cyan1)
            .Title("[bold cyan]azure-devops subcommands[/]")
            .AddColumn(new TableColumn("[bold]Subcommand[/]").NoWrap())
            .AddColumn(new TableColumn("[bold]Description[/]"));

        table.AddRow("repo new", "Create a new repository interactively");
        table.AddRow("repo checkout", "Clone a repository interactively");
        table.AddRow("variable-group list", "List and inspect variable groups");
        table.AddRow("hu task", "Create tasks for a User Story");
        table.AddRow("hu task <org> <proj> <hu-id>", "Create tasks (skip menus)");
        table.AddRow("hu list", "List tasks of a User Story");
        table.AddRow("hu list <org> <proj> <hu-id>", "List tasks (skip menus)");
        table.AddRow("link", "Add link (branch/commit/PR) to work item");
        table.AddRow("link <org> <proj> <wi-id>", "Add link (skip menus)");

        AnsiConsole.Write(table);

        AnsiConsole.MarkupLine("\n[bold]Examples:[/]");
        AnsiConsole.MarkupLine("  yitpush azure-devops hu task MyOrg MyProj 123 [dim]# Create tasks (quick)[/]");
        AnsiConsole.MarkupLine("  yitpush azure-devops hu list MyOrg MyProj 123 [dim]# List tasks of HU[/]");
        AnsiConsole.MarkupLine("  yitpush azure-devops link MyOrg MyProj 123    [dim]# Add link to work item[/]");
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


        // Fetch latest remote branches
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
            AnsiConsole.MarkupLine($"[green]Switched to '{branchName}'[/]");
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
                    AnsiConsole.MarkupLine($"[green]Switched to '{localName}'[/]");
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

    private static async Task<bool?> EnsureAzureDevOpsReady()
    {
        var azCheck = await RunAzCapture("--version");
        if (azCheck == null)
        {
            AnsiConsole.MarkupLine("[red]Azure CLI (az) is not installed.[/]");
            return null;
        }

        var extCheck = await RunAzCapture("extension show --name azure-devops --output json");
        if (extCheck == null)
        {
            var installResult = await RunAzPassthrough("extension add --name azure-devops");
            if (!installResult)
            {
                AnsiConsole.MarkupLine("[red]Failed to install Azure DevOps extension.[/]");
                return null;
            }
        }

        var accountJson = await RunAzCapture("account show --output json");
        if (accountJson == null)
        {
            var loginResult = await RunAzPassthrough("login");
            if (!loginResult)
            {
                AnsiConsole.MarkupLine("[red]Azure login failed.[/]");
                return null;
            }
        }

        return true;
    }

    private static async Task<(string OrgUrl, string Project)?> EnsureAzureDevOpsSetup()
    {
        var ready = await EnsureAzureDevOpsReady();
        if (ready == null) return null;

        // 4. List organizations via Azure DevOps REST API
        var organizations = await FetchAzureOrganizations();

        if (organizations.Count == 0)
        {
            var orgName = AnsiConsole.Prompt(
                new TextPrompt<string>("Organization name:")
                    .PromptStyle(new Style(Color.Cyan1))
                    .AllowEmpty());

            if (string.IsNullOrWhiteSpace(orgName)) return null;

            organizations.Add(orgName.Trim());
        }

        organizations.Sort(StringComparer.OrdinalIgnoreCase);

        while (true)
        {
            var orgChoices = new List<string>(organizations) { BackOption };
            var selectedOrg = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title("Organization:")
                    .PageSize(10)
                    .HighlightStyle(new Style(Color.Cyan1))
                    .AddChoices(orgChoices));

            if (selectedOrg == BackOption) return null;

            var orgUrl = $"https://dev.azure.com/{selectedOrg}";
            var projects = await FetchAzureProjects(orgUrl);

            if (projects.Count == 0)
            {
                AnsiConsole.MarkupLine("[red]No projects found.[/]");
                return null;
            }

            projects.Sort(StringComparer.OrdinalIgnoreCase);
            var projectChoices = new List<string>(projects) { BackOption };
            var selectedProject = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title("Project:")
                    .PageSize(10)
                    .HighlightStyle(new Style(Color.Cyan1))
                    .AddChoices(projectChoices));

            if (selectedProject == BackOption) continue;

            return (orgUrl, selectedProject);
        }
    }

    private static async Task<string?> CreateAzureDevOpsRepo()
    {

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

                AnsiConsole.MarkupLine($"[green]Created:[/] {remoteUrl}");
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

            AnsiConsole.MarkupLine($"[green]Remote '{finalRemoteName}':[/] {remoteUrl}");

            return finalRemoteName;
        }
    }

    private static async Task<int> CheckoutAzureDevOpsRepo()
    {

        var setup = await EnsureAzureDevOpsSetup();
        if (setup == null) return 1;

        var (orgUrl, selectedProject) = setup.Value;

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

        if (selectedRepoName == BackOption) return BackToMenu;

        var selectedRepo = repos.First(r => r.Name == selectedRepoName);

        var defaultDir = Path.Combine(Directory.GetCurrentDirectory(), selectedRepo.Name);
        var targetDir = AnsiConsole.Prompt(
            new TextPrompt<string>("Clone into:")
                .DefaultValue(defaultDir)
                .PromptStyle(new Style(Color.Cyan1)));
        var success = await RunCommandPassthrough("git", $"clone \"{selectedRepo.RemoteUrl}\" \"{targetDir}\"");

        if (!success)
        {
            AnsiConsole.MarkupLine("[red]❌ Failed to clone repository.[/]");
            return 1;
        }

        AnsiConsole.MarkupLine($"[green]Cloned into:[/] {targetDir}");
        return 0;
    }

    private static async Task<int> ListAzureUserStories()
    {

        var setup = await EnsureAzureDevOpsSetup();
        if (setup == null) return 1;

        var (orgUrl, project) = setup.Value;

        var hus = await FetchAzureUserStories(orgUrl, project);

        if (hus.Count == 0)
        {
            AnsiConsole.MarkupLine("[yellow]No User Stories found.[/]");
            return 0;
        }

        var maxIdLen = hus.Max(h => h.Id.Length);
        var displayMap = new Dictionary<string, (string Id, string Title, string State, string Date, string Area, string Iteration)>();
        var displayList = new List<string>();

        foreach (var hu in hus)
        {
            var display = $"[grey]{hu.Date}[/] [cyan]{hu.Id.PadRight(maxIdLen + 1)}[/] {Markup.Escape(hu.Title)} [dim]({Markup.Escape(hu.State)})[/]";
            displayMap[display] = hu;
            displayList.Add(display);
        }

        while (true)
        {
            var choices = new List<string>(displayList) { BackOption };
            var selected = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title("User Story:")
                    .PageSize(15)
                    .HighlightStyle(new Style(Color.Cyan1))
                    .AddChoices(choices));

            if (selected == BackOption) return BackToMenu;

            var selectedHu = displayMap[selected];

            var action = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title("What would you like to do?")
                    .AddChoices("Create standard tasks", "Create branch for this HU", "Mark as In Progress", "Add link to repo", BackOption));

            if (action == BackOption) continue;

            if (action == "Create standard tasks")
            {
                await CreateTasksForUserStory(orgUrl, project, selectedHu.Id, selectedHu.Area, selectedHu.Iteration);
            }
            else if (action == "Create branch for this HU")
            {
                var branchName = $"feature/{selectedHu.Id}-{selectedHu.Title.ToLower().Replace(" ", "-").Replace("\"", "")}";
                branchName = new string(branchName.Where(c => char.IsLetterOrDigit(c) || c == '/' || c == '-').ToArray());
                
                var success = await RunCommandPassthrough("git", $"checkout -b {branchName}");
                if (success)
                    AnsiConsole.MarkupLine($"[green]Branch:[/] {branchName}");
            }
            else if (action == "Mark as In Progress")
            {
                var success = await RunAzPassthrough(
                    $"boards work-item update --id {selectedHu.Id} --state \"In Progress\" --organization {orgUrl}");
                if (success)
                    AnsiConsole.MarkupLine($"[green]Status updated.[/]");
            }
            else if (action == "Add link to repo")
            {
                await AddLinkToRepo(orgUrl, project, selectedHu.Id);
            }
        }
    }

    private static async Task<int> CreateTasksForUserStory(string orgUrl, string project, string huId, string areaPath, string iterationPath)
    {
        var taskTitles = AnsiConsole.Prompt(
            new TextPrompt<string>("Enter task titles (comma separated):")
                .DefaultValue("Desarrollo, Pruebas Unitarias, Code Review"));

        var titles = taskTitles.Split(',').Select(t => t.Trim()).Where(t => !string.IsNullOrEmpty(t));

        string[] meses = { "Enero", "Febrero", "Marzo", "Abril", "Mayo", "Junio", "Julio", "Agosto", "Septiembre", "Octubre", "Noviembre", "Diciembre" };
        string currentMonth = meses[DateTime.Now.Month - 1];

        // Get current user for assignment
        var userEmail = await GetCurrentUserEmail();
        string assignedField = "";
        if (!string.IsNullOrEmpty(userEmail))
        {
            assignedField = $" \"System.AssignedTo={userEmail}\"";
        }

        // Ask for estimated effort
        var effortHours = AnsiConsole.Prompt(
            new TextPrompt<string>("Estimated effort in hours (default: 1):")
                .DefaultValue("1")
                .AllowEmpty());

        if (string.IsNullOrWhiteSpace(effortHours)) effortHours = "1";

        foreach (var title in titles)
        {
            AnsiConsole.Markup($"[dim]{Markup.Escape(title)}...[/] ");
            
            // Initial fields based on SAG project requirements
            var fields = $"\"Microsoft.VSTS.Scheduling.RemainingWork=0\" \"Custom.EsfuerzoEstimadoHH={effortHours}\" \"Custom.Mes={currentMonth}\"{assignedField}";
            
            string? createJson = null;
            string? createError = null;
            bool success = false;

            while (!success)
            {
                (createJson, createError) = await RunAzCaptureWithError(
                    $"boards work-item create --title \"{title}\" --type Task --project \"{project}\" --area \"{areaPath}\" --iteration \"{iterationPath}\" --fields {fields} --organization {orgUrl} --output json");

                if (createJson != null)
                {
                    success = true;
                }
                else if (createError != null && (createError.Contains("TF401320") || createError.Contains("TF51535")))
                {
                    if (createError.Contains("TF401320")) // Mandatory field missing
                    {
                        AnsiConsole.MarkupLine("[yellow]⚠️  Missing mandatory field detected.[/]");
                        var match = Regex.Match(createError, @"field ([^.]+)\.");
                        if (match.Success)
                        {
                            var fieldName = match.Groups[1].Value;
                            var val = AnsiConsole.Prompt(
                                new TextPrompt<string>($"Field [yellow]{fieldName}[/] is required. Enter value:")
                                    .DefaultValue(fieldName == "Activity" ? "Development" : "1"));
                            
                            // Guess internal name if needed
                            var internalName = fieldName;
                            if (fieldName == "Mes" || fieldName == "EsfuerzoEstimadoHH") internalName = "Custom." + fieldName;
                            else if (fieldName == "Activity" || fieldName == "Priority") internalName = "Microsoft.VSTS.Common." + fieldName;

                            fields += $" \"{internalName}={val}\"";
                            AnsiConsole.MarkupLine("🔄 Retrying with added field...");
                        }
                        else
                        {
                            AnsiConsole.MarkupLine($"[dim]Error: {Markup.Escape(createError.Trim())}[/]");
                            var extraFields = AnsiConsole.Prompt(
                                new TextPrompt<string>("Enter missing fields as 'Field=Value' pairs:")
                                    .AllowEmpty());
                            if (string.IsNullOrEmpty(extraFields)) break;
                            fields += " " + extraFields;
                        }
                    }
                    else // TF51535: Cannot find field
                    {
                        AnsiConsole.MarkupLine("[red]❌ Error: Azure DevOps cannot find one of the fields.[/]");
                        AnsiConsole.MarkupLine($"[dim]{Markup.Escape(createError.Trim())}[/]");
                        var corrected = AnsiConsole.Prompt(
                            new TextPrompt<string>("Re-enter ALL field=value pairs correctly (or leave empty to skip):")
                                .AllowEmpty());
                        if (string.IsNullOrEmpty(corrected)) break;
                        fields = corrected;
                    }
                }
                else
                {
                    AnsiConsole.MarkupLine($"[red]❌ Unexpected error creating task:[/] [dim]{Markup.Escape(createError ?? "Unknown error")}[/]");
                    break;
                }
            }

            if (createJson != null)
            {
                string? taskId = null;
                try
                {
                    using var doc = JsonDocument.Parse(createJson);
                    if (doc.RootElement.TryGetProperty("id", out var idProp))
                    {
                        taskId = idProp.ToString();
                    }
                }
                catch { }

                if (taskId != null)
                {
                    AnsiConsole.Markup($"[green]#{taskId}[/] ");
                    
                    var linkSuccess = await RunAzPassthrough(
                        $"boards work-item relation add --id {taskId} --relation-type parent --target-id {huId} --organization {orgUrl} --output none");
                    
                    if (linkSuccess)
                        AnsiConsole.MarkupLine($"[green]linked[/]");
                    else
                        AnsiConsole.MarkupLine($"[yellow]created but not linked[/]");
                }
            }
        }

        return 0;
    }

    private static async Task<int> CreateTasksDirectForHU(string orgUrl, string project, string huId)
    {
        var setup = await EnsureAzureDevOpsReady();
        if (setup == null) return 1;

        var wiJson = await RunAzCapture(
            $"boards work-item show --id {huId} --organization {orgUrl} --output json");
        if (wiJson == null)
        {
            AnsiConsole.MarkupLine($"[red]HU {huId} not found.[/]");
            return 1;
        }

        string areaPath = project, iterationPath = project;
        try
        {
            using var doc = JsonDocument.Parse(wiJson);
            var fields = doc.RootElement.GetProperty("fields");
            if (fields.TryGetProperty("System.AreaPath", out var ap)) areaPath = ap.GetString() ?? areaPath;
            if (fields.TryGetProperty("System.IterationPath", out var ip)) iterationPath = ip.GetString() ?? iterationPath;
            var title = fields.TryGetProperty("System.Title", out var tp) ? tp.GetString() : "";
            AnsiConsole.MarkupLine($"[cyan]HU {huId}:[/] {Markup.Escape(title ?? "")}");
        }
        catch { }

        return await CreateTasksForUserStory(orgUrl, project, huId, areaPath, iterationPath);
    }

    private static async Task<int> ListTasksForHU(string orgUrl, string project, string huId)
    {
        var setup = await EnsureAzureDevOpsReady();
        if (setup == null) return 1;

        // Fetch the HU work item to get its child relations
        var wiJson = await RunAzCapture(
            $"boards work-item show --id {huId} --organization {orgUrl} --expand relations --output json");

        if (wiJson == null)
        {
            AnsiConsole.MarkupLine($"[red]Failed to fetch HU {huId}.[/]");
            return 1;
        }

        var childIds = new List<string>();
        try
        {
            using var doc = JsonDocument.Parse(wiJson);
            if (doc.RootElement.TryGetProperty("relations", out var relations) && relations.ValueKind == JsonValueKind.Array)
            {
                foreach (var rel in relations.EnumerateArray())
                {
                    var relType = rel.TryGetProperty("rel", out var rp) ? rp.GetString() : "";
                    if (relType == "System.LinkTypes.Hierarchy-Forward") // child
                    {
                        var url = rel.TryGetProperty("url", out var up) ? up.GetString() : "";
                        if (url != null)
                        {
                            var lastSlash = url.LastIndexOf('/');
                            if (lastSlash >= 0)
                                childIds.Add(url[(lastSlash + 1)..]);
                        }
                    }
                }
            }
        }
        catch { }

        var tasks = new List<(string Id, string Title, string State)>();
        foreach (var id in childIds)
        {
            var detailJson = await RunAzCapture(
                $"boards work-item show --id {id} --organization {orgUrl} --output json");
            string title = id, state = "";
            if (detailJson != null)
            {
                try
                {
                    using var detailDoc = JsonDocument.Parse(detailJson);
                    var fields = detailDoc.RootElement.GetProperty("fields");
                    title = fields.TryGetProperty("System.Title", out var tp) ? tp.GetString() ?? id : id;
                    state = fields.TryGetProperty("System.State", out var sp) ? sp.GetString() ?? "" : "";
                }
                catch { }
            }
            tasks.Add((id, title, state));
        }

        if (tasks.Count == 0)
        {
            AnsiConsole.MarkupLine($"[yellow]No tasks found for HU {huId}.[/]");
            return 0;
        }

        foreach (var t in tasks)
        {
            var stateColor = t.State switch
            {
                "Closed" or "Done" => "green",
                "In Progress" or "Active" => "cyan",
                _ => "dim"
            };
            AnsiConsole.MarkupLine($"  [{stateColor}]{t.Id}[/] {Markup.Escape(t.Title)} [dim]({Markup.Escape(t.State)})[/]");
        }

        while (true)
        {
            var taskChoices = tasks.Select(t => $"{t.Id} - {t.Title}").ToList();
            taskChoices.Add(BackOption);
            var selected = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title("Add link to task:")
                    .PageSize(15)
                    .HighlightStyle(new Style(Color.Cyan1))
                    .AddChoices(taskChoices));

            if (selected == BackOption) return 0;

            var taskId = selected.Split(" - ")[0].Trim();
            await AddLinkToRepo(orgUrl, project, taskId);
        }
    }

    private static async Task<int> ListAzureUserStoriesForTaskList()
    {
        var setup = await EnsureAzureDevOpsSetup();
        if (setup == null) return 1;

        var (orgUrl, project) = setup.Value;
        var hus = await FetchAzureUserStories(orgUrl, project);

        if (hus.Count == 0)
        {
            AnsiConsole.MarkupLine("[yellow]No User Stories found.[/]");
            return 0;
        }

        var maxIdLen = hus.Max(h => h.Id.Length);
        var displayMap = new Dictionary<string, string>();
        var displayList = new List<string>();

        foreach (var hu in hus)
        {
            var display = $"[cyan]{hu.Id.PadRight(maxIdLen + 1)}[/] {Markup.Escape(hu.Title)} [dim]({Markup.Escape(hu.State)})[/]";
            displayMap[display] = hu.Id;
            displayList.Add(display);
        }

        while (true)
        {
            var choices = new List<string>(displayList) { BackOption };
            var selected = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title("User Story:")
                    .PageSize(15)
                    .HighlightStyle(new Style(Color.Cyan1))
                    .AddChoices(choices));

            if (selected == BackOption) return BackToMenu;

            var huId = displayMap[selected];
            await ListTasksForHU(orgUrl, project, huId);
        }
    }

    private static async Task<int> AddLinkToRepo(string orgUrl, string project, string workItemId)
    {

        // Fetch repositories
        var repos = await FetchAzureRepos(orgUrl, project);
        if (repos.Count == 0)
        {
            AnsiConsole.MarkupLine("[red]❌ No repositories found in this project.[/]");
            return 1;
        }

        repos.Sort((a, b) => StringComparer.OrdinalIgnoreCase.Compare(a.Name, b.Name));
        var repoChoices = new List<string>(repos.Select(r => r.Name)) { BackOption };
        var selectedRepoName = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("📋 Select [green]repository[/]:\n[dim](Select ← Back to return)[/]")
                .PageSize(15)
                .HighlightStyle(new Style(Color.Cyan1))
                .AddChoices(repoChoices));

        if (selectedRepoName == BackOption) return 0;

        var selectedRepo = repos.First(r => r.Name == selectedRepoName);
        // Ask for link type
        var linkType = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("Link type:")
                .AddChoices("Branch", "Commit", "Pull Request", BackOption));

        if (linkType == BackOption) return 0;

        string artifactUrl = "";
        if (linkType == "Branch")
        {
            // Try to fetch branches
            var branchesJson = await RunAzCapture($"repos ref list --repository \"{selectedRepo.Name}\" --organization {orgUrl} --project \"{project}\" --output json");
            var branches = new List<string>();
            if (branchesJson != null)
            {
                try
                {
                    using var doc = JsonDocument.Parse(branchesJson);
                    JsonElement items;
                    if (doc.RootElement.ValueKind == JsonValueKind.Array)
                        items = doc.RootElement;
                    else if (doc.RootElement.TryGetProperty("value", out var valueProp))
                        items = valueProp;
                    else
                        items = default;

                    if (items.ValueKind != JsonValueKind.Undefined && items.ValueKind != JsonValueKind.Null)
                    {
                        foreach (var branch in items.EnumerateArray())
                        {
                            if (branch.TryGetProperty("name", out var nameProp))
                            {
                                var name = nameProp.GetString();
                                if (name != null && name.StartsWith("refs/heads/"))
                                    branches.Add(name["refs/heads/".Length..]);
                            }
                        }
                    }
                }
                catch { }
            }

            if (branches.Count > 0)
            {
                branches.Sort();
                var branchChoices = new List<string>(branches) { BackOption, "Enter custom branch name" };
                var selectedBranch = AnsiConsole.Prompt(
                    new SelectionPrompt<string>()
                        .Title("Select [green]branch[/]:")
                        .PageSize(15)
                        .AddChoices(branchChoices));

                if (selectedBranch == BackOption) return 0;
                if (selectedBranch == "Enter custom branch name")
                {
                    var customBranch = AnsiConsole.Prompt(
                        new TextPrompt<string>("Enter branch name (without refs/heads/):")
                            .DefaultValue("main"));
                    artifactUrl = $"{orgUrl}/{Uri.EscapeDataString(project)}/_git/{Uri.EscapeDataString(selectedRepo.Name)}?version=GB{Uri.EscapeDataString(customBranch)}";
                }
                else
                {
                    artifactUrl = $"{orgUrl}/{Uri.EscapeDataString(project)}/_git/{Uri.EscapeDataString(selectedRepo.Name)}?version=GB{Uri.EscapeDataString(selectedBranch)}";
                }
            }
            else
            {
                // No branches found or error, ask for branch name
                var branchName = AnsiConsole.Prompt(
                    new TextPrompt<string>("Enter branch name (without refs/heads/):")
                        .DefaultValue("main"));
                artifactUrl = $"{orgUrl}/{Uri.EscapeDataString(project)}/_git/{Uri.EscapeDataString(selectedRepo.Name)}?version=GB{Uri.EscapeDataString(branchName)}";
            }
        }
        else if (linkType == "Commit")
        {
            var commitHash = AnsiConsole.Prompt(
                new TextPrompt<string>("Enter commit hash (full or short):")
                    .ValidationErrorMessage("Commit hash is required")
                    .Validate(hash => !string.IsNullOrWhiteSpace(hash)));
            
            artifactUrl = $"{orgUrl}/{Uri.EscapeDataString(project)}/_git/{Uri.EscapeDataString(selectedRepo.Name)}/commit/{commitHash}";
        }
        else if (linkType == "Pull Request")
        {
            var prId = AnsiConsole.Prompt(
                new TextPrompt<string>("Enter Pull Request ID:")
                    .ValidationErrorMessage("PR ID is required")
                    .Validate(id => int.TryParse(id, out _)));
            
            artifactUrl = $"{orgUrl}/{Uri.EscapeDataString(project)}/_git/{Uri.EscapeDataString(selectedRepo.Name)}/pullrequest/{prId}";
        }
        if (string.IsNullOrEmpty(artifactUrl))
        {
            AnsiConsole.MarkupLine("[red]❌ Could not build artifact URL.[/]");
            return 1;
        }

        var (result, linkError) = await RunAzCaptureWithError(
            $"boards work-item relation add --id {workItemId} --relation-type Hyperlink --target-url \"{artifactUrl}\" --organization {orgUrl} --output json");

        if (result != null)
            AnsiConsole.MarkupLine($"[green]Link added.[/]");
        else
            AnsiConsole.MarkupLine($"[red]Failed to add link.[/] [dim]{Markup.Escape(linkError?.Trim() ?? "")}[/]");

        return 0;
    }

    private static async Task<int> ListAzureVariableGroups()
    {

        var setup = await EnsureAzureDevOpsSetup();
        if (setup == null) return 1;

        var (orgUrl, project) = setup.Value;

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

                if (selected == BackOption) return BackToMenu;

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
        AnsiConsole.MarkupLine("[yellow]Azure session expired.[/]");
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

        AnsiConsole.MarkupLine("[green]Re-authenticated.[/]");
        return true;
    }

    private static async Task<List<string>> FetchAzureOrganizations()
    {
        var organizations = new List<string>();
        const string azDevOpsResource = "499b84ac-1321-427f-aa17-267ca6975798";

        // Get user profile to obtain memberId
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

    private static async Task<List<(string Id, string Title, string State, string Date, string Area, string Iteration)>> FetchAzureUserStories(string orgUrl, string project)
    {
        var stories = new List<(string Id, string Title, string State, string Date, string Area, string Iteration)>();
        var huTypes = "([System.WorkItemType] = 'User Story' OR [System.WorkItemType] = 'Historia de usuario' OR [System.WorkItemType] = 'Product Backlog Item')";

        // Try to get current user email
        var accountJson = await RunAzCapture("account show --output json");
        string? userEmail = null;
        if (accountJson != null)
        {
            try {
                using var doc = JsonDocument.Parse(accountJson);
                userEmail = doc.RootElement.TryGetProperty("user", out var userProp)
                    && userProp.TryGetProperty("name", out var nameProp)
                    ? nameProp.GetString() : null;
            } catch {}
        }
        
        var userFilter = "@Me";
        if (!string.IsNullOrEmpty(userEmail))
        {
            userFilter = $"@Me, '{userEmail}'";
        }

        // 1. Search for HUs where user is AssignedTo or Desarrollador
        var developerFields = new[] { "Custom.Desarrollador", "Custom.Developer", "Microsoft.VSTS.Common.Developer" };
        
        foreach (var devField in developerFields)
        {
            var query = $"SELECT [System.Id], [System.Title], [System.State], [System.CreatedDate], [System.AreaPath], [System.IterationPath] FROM WorkItems WHERE [System.TeamProject] = '{project}' AND {huTypes} AND ([System.AssignedTo] IN ({userFilter}) OR [{devField}] IN ({userFilter})) ORDER BY [System.CreatedDate] DESC";
            var results = await ExecuteWiqlQuery(orgUrl, project, query, silent: true);
            foreach (var r in results)
            {
                if (!stories.Any(s => s.Id == r.Id)) stories.Add(r);
            }
        }

        // 2. If no HUs found directly, find HUs that are PARENTS of tasks assigned to the user
        if (stories.Count == 0)
        {
            // This query finds the IDs of parents (HUs) of tasks assigned to user
            var taskQuery = $"SELECT [System.Parent] FROM WorkItems WHERE [System.TeamProject] = '{project}' AND [System.WorkItemType] = 'Task' AND [System.AssignedTo] IN ({userFilter})";
            var tasks = await ExecuteWiqlQuery(orgUrl, project, taskQuery, silent: true);
            var parentIds = tasks.Select(t => t.Id).Where(p => p != "-" && p != "0").Distinct().ToList();

            if (parentIds.Count > 0)
            {
                var idList = string.Join(",", parentIds);
                var fetchParentsQuery = $"SELECT [System.Id], [System.Title], [System.State], [System.CreatedDate], [System.AreaPath], [System.IterationPath] FROM WorkItems WHERE [System.Id] IN ({idList}) AND {huTypes} ORDER BY [System.CreatedDate] DESC";
                var parents = await ExecuteWiqlQuery(orgUrl, project, fetchParentsQuery);
                stories.AddRange(parents);
            }
        }

        // 3. Last fallback: Any HU in the project
        if (stories.Count == 0)
        {
             var recentQuery = $"SELECT [System.Id], [System.Title], [System.State], [System.CreatedDate], [System.AreaPath], [System.IterationPath] FROM WorkItems WHERE [System.TeamProject] = '{project}' AND {huTypes} ORDER BY [System.CreatedDate] DESC";
             stories = await ExecuteWiqlQuery(orgUrl, project, recentQuery);
        }

        return stories;
    }

    private static async Task<List<(string Id, string Title, string State, string Date, string Area, string Iteration)>> ExecuteWiqlQuery(string orgUrl, string project, string wiql, bool silent = false)
    {
        var results = new List<(string Id, string Title, string State, string Date, string Area, string Iteration)>();
        var escapedWiql = wiql.Replace("\"", "\\\"");
        
        var (json, error) = await RunAzCaptureWithError(
            $"boards query --wiql \"{escapedWiql}\" --organization {orgUrl} --project \"{project}\" --output json");

        if (json == null) 
        {
            if (!silent && !string.IsNullOrEmpty(error))
                AnsiConsole.MarkupLine($"[red]❌ Query error:[/] {Markup.Escape(error)}");
            return results;
        }

        try
        {
            using var doc = JsonDocument.Parse(json);
            JsonElement items;
            if (doc.RootElement.ValueKind == JsonValueKind.Array)
                items = doc.RootElement;
            else if (doc.RootElement.TryGetProperty("value", out var valueProp))
                items = valueProp;
            else
                return results;

            foreach (var item in items.EnumerateArray())
            {
                var fields = item.TryGetProperty("fields", out var fieldsProp) ? fieldsProp : item;
                
                var id = item.TryGetProperty("id", out var idProp) ? idProp.ToString() : "-";
                
                // Get Title or first available text field
                string? title = null;
                if (fields.TryGetProperty("System.Title", out var titleProp)) title = titleProp.GetString();
                else if (fields.TryGetProperty("System.Parent", out var parentProp)) title = parentProp.ToString(); // Special case for when we just want the parent ID
                
                var state = fields.TryGetProperty("System.State", out var stateProp) ? stateProp.GetString() : "-";
                var createdDate = fields.TryGetProperty("System.CreatedDate", out var dateProp) ? dateProp.GetString() : "-";
                var areaPath = fields.TryGetProperty("System.AreaPath", out var areaProp) ? areaProp.GetString() : "-";
                var iterationPath = fields.TryGetProperty("System.IterationPath", out var iterationProp) ? iterationProp.GetString() : "-";
                
                if (id != "-")
                {
                    // Format date to something short: YYYY-MM-DD
                    var dateStr = createdDate != "-" && DateTime.TryParse(createdDate, out var dt) 
                        ? dt.ToString("yyyy-MM-dd") 
                        : createdDate;

                    results.Add((id, title ?? "-", state ?? "-", dateStr ?? "-", areaPath ?? "-", iterationPath ?? "-"));
                }
            }
        }
        catch { }

        return results;
    }

    // ─── Process helpers ──────────────────────────────────────────────────────

    private static async Task<string?> GetCurrentUserEmail()
    {
        var accountJson = await RunAzCapture("account show --output json");
        if (accountJson != null)
        {
            try
            {
                using var doc = JsonDocument.Parse(accountJson);
                if (doc.RootElement.TryGetProperty("user", out var userProp) &&
                    userProp.TryGetProperty("name", out var nameProp))
                {
                    return nameProp.GetString();
                }
            }
            catch { }
        }
        return null;
    }

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
