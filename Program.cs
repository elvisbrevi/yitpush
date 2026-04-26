using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json;
using Spectre.Console;

namespace YitPush;

partial class Program
{
    private const string AzDevOpsResource = "499b84ac-1321-427f-aa17-267ca6975798";
    private const string DeepSeekApiUrl = "https://api.deepseek.com/v1/chat/completions";
    private const string DeepSeekModel = "deepseek-chat";
    private const string BackOption = "← Back";
    private const int BackToMenu = 100;

    private const int ApiMaxTokens = 8000;
    private const int ApiTimeoutSeconds = 120;
    private const int ApiMaxContextTokens = 131072;

    // Azure DevOps field names
    private const string AzFieldEffortHH = "Custom.EsfuerzoEstimadoHH";
    private const string AzFieldEffortRealHH = "Custom.EsfuerzoRealHH";
    private const string AzFieldRemainingWork = "Microsoft.VSTS.Scheduling.RemainingWork";

    static async Task<int> Main(string[] args)
    {
        try
        {
            Console.OutputEncoding = Encoding.UTF8;
            Console.InputEncoding = Encoding.UTF8;
        }
        catch { }

        Console.WriteLine();

        // Start version check in background — won't block the main command
        var versionCheckTask = Task.Run(CheckForUpdates);

        if (args.Length == 0 || args[0] is "--help" or "-h" or "-help" or "help")
        {
            ShowHelp();
            await versionCheckTask;
            return 0;
        }

        int result;
        switch (args[0])
        {
            case "commit":
                result = await CommitCommand(args.Skip(1).ToArray()); break;
            case "checkout":
                result = await CheckoutBranch(); break;
            case "pr":
                result = await PrCommand(args.Skip(1).ToArray()); break;
            case "setup":
                result = await SetupCommand(); break;
            case "skill":
                result = await InstallSkillCommand(); break;
            case "azure-devops":
                result = await AzureDevOpsCommand(args.Skip(1).ToArray()); break;
            default:
                AnsiConsole.MarkupLine($"[red]❌ Unknown command:[/] {Markup.Escape(args[0])}\n");
                ShowHelp();
                result = 1; break;
        }

        await versionCheckTask;
        return result;
    }

    // ─── Help ─────────────────────────────────────────────────────────────────

    private static void ShowHelp()
    {
        AnsiConsole.MarkupLine("[bold]Usage:[/] yp [bold]<command>[/] [[options]]\n");

        var commandsTable = new Table()
            .Border(TableBorder.Rounded)
            .BorderColor(Color.Cyan1)
            .Title("[bold cyan]Commands[/]")
            .AddColumn(new TableColumn("[bold]Command[/]").NoWrap())
            .AddColumn(new TableColumn("[bold]Description[/]"));

        commandsTable.AddRow("setup", "Configure AI provider (OpenAI, Anthropic, Google, DeepSeek, OpenRouter)");
        commandsTable.AddRow("commit", "Stage, commit and push changes with an AI-generated message");
        commandsTable.AddRow("pr", "Generate a pull request description between two branches");
        commandsTable.AddRow("checkout", "Interactive branch checkout");
        commandsTable.AddRow("azure-devops", "Manage Azure DevOps resources");
        commandsTable.AddRow("skill", "Install the yp skill for your AI agent (Claude Code, Cursor, Gemini CLI...)");

        AnsiConsole.Write(commandsTable);

        AnsiConsole.MarkupLine("\n[bold cyan]commit[/] options:");
        var commitTable = new Table()
            .Border(TableBorder.Rounded)
            .BorderColor(Color.Grey)
            .AddColumn(new TableColumn("[bold]Flag[/]").NoWrap())
            .AddColumn(new TableColumn("[bold]Description[/]"));

        commitTable.AddRow("--confirm", "Ask for confirmation before committing");
        commitTable.AddRow("--detailed", "Generate detailed commit with title + body");
        commitTable.AddRow("--language <lang>, --lang, -l", "Output language (e.g., english, spanish, french)");
        commitTable.AddRow("--save", "Save commit message to a markdown file");

        AnsiConsole.Write(commitTable);

        AnsiConsole.MarkupLine("\n[bold cyan]pr[/] options:");
        var prTable = new Table()
            .Border(TableBorder.Rounded)
            .BorderColor(Color.Grey)
            .AddColumn(new TableColumn("[bold]Flag[/]").NoWrap())
            .AddColumn(new TableColumn("[bold]Description[/]"));

        prTable.AddRow("--detailed", "Generate detailed PR description");
        prTable.AddRow("--language <lang>, --lang, -l", "Output language (e.g., english, spanish, french)");
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
        azTable.AddRow("hu task <org> <proj> <hu-id> [[--description \"...\"]] [[--effort \"...\"]]", "Create tasks (skip menus)");
        azTable.AddRow("hu show", "Show User Story details");
        azTable.AddRow("hu show <org> <hu-id>", "Show details (skip menus)");
        azTable.AddRow("hu list", "List tasks of a User Story");
        azTable.AddRow("hu list <org> <proj> <hu-id>", "List tasks (skip menus)");
        azTable.AddRow("hu link", "Link a repository branch to a User Story");
        azTable.AddRow("hu link <org> <proj> <hu-id> --repo <repo> --branch <branch>", "Link branch (skip menus)");
        azTable.AddRow("task show", "Show task details");
        azTable.AddRow("task show <org> <id>", "Show details (skip menus)");
        azTable.AddRow("task update", "Update effort, remaining, state or comment (alias: hu update, wi update)");
        azTable.AddRow("task update <org> <id> [[--effort|-e <e>]] [[--effort-real|-er <er>]] [[--remaining|-r <r>]] [[--state|-s <s>]] [[--comment|-c <c>]]", "Update directly");
        azTable.AddRow("link", "Add link (branch/commit/PR) to work item");
        azTable.AddRow("link <org> <proj> <wi-id>", "Add link (skip menus)");

        AnsiConsole.Write(azTable);

        AnsiConsole.MarkupLine("\n[dim]Short flags for hu task: --description|-d, --effort|-e[/]");

        AnsiConsole.MarkupLine("\n[bold]Examples:[/]");
        AnsiConsole.MarkupLine("  yp setup                                        [dim]# Configure AI provider[/]");
        AnsiConsole.MarkupLine("  yp skill                                        [dim]# Install skill for your AI agent[/]");
        AnsiConsole.MarkupLine("  yp commit                                       [dim]# Auto commit and push[/]");
        AnsiConsole.MarkupLine("  yp commit --confirm                             [dim]# Review before committing[/]");
        AnsiConsole.MarkupLine("  yp commit --detailed -l spanish                 [dim]# Detailed commit in Spanish[/]");
        AnsiConsole.MarkupLine("  yp pr                                           [dim]# Generate PR description[/]");
        AnsiConsole.MarkupLine("  yp pr --detailed -l french                      [dim]# Detailed PR description in French[/]");
        AnsiConsole.MarkupLine("  yp checkout                                     [dim]# Switch branch interactively[/]");
        AnsiConsole.MarkupLine("  yp azure-devops hu task                         [dim]# Create tasks interactively[/]");
        AnsiConsole.MarkupLine("  yp azure-devops hu show MyOrg 12345             [dim]# Show HU details[/]");
        AnsiConsole.MarkupLine("  yp azure-devops task show MyOrg 67890           [dim]# Show Task details[/]");
        AnsiConsole.MarkupLine("  yp azure-devops task update MyOrg 67890 --state \"Doing\" --effort-real \"3\"  [dim]# Update task[/]");
        AnsiConsole.MarkupLine("  yp azure-devops hu link MyOrg MyProj 12345 --repo MyRepo --branch feature/abc  [dim]# Link branch[/]");
        AnsiConsole.MarkupLine("  yp azure-devops hu list MyOrg MyProj 12345      [dim]# List tasks of HU[/]");
        AnsiConsole.MarkupLine("  yp azure-devops link MyOrg MyProj 12345         [dim]# Add link to work item[/]");
        Console.WriteLine();
    }


    // ─── Version check ────────────────────────────────────────────────────────

    private static async Task CheckForUpdates()
    {
        try
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var cacheFile = Path.Combine(home, ".yitpush", "version-check.json");

            string? latestVersion = null;

            // Read cache — check at most once per day
            if (File.Exists(cacheFile))
            {
                var cacheJson = await File.ReadAllTextAsync(cacheFile);
                var cache = JsonSerializer.Deserialize<VersionCheckCache>(cacheJson,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (cache != null && (DateTime.UtcNow - cache.LastCheck).TotalHours < 24)
                {
                    latestVersion = cache.LatestVersion;
                }
            }

            // Fetch from NuGet if cache is stale or missing
            if (latestVersion == null)
            {
                using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
                var json = await http.GetStringAsync(
                    "https://api.nuget.org/v3-flatcontainer/yitpush/index.json");
                var doc = JsonDocument.Parse(json);
                latestVersion = doc.RootElement
                    .GetProperty("versions")
                    .EnumerateArray()
                    .Select(v => v.GetString() ?? "")
                    .LastOrDefault(v => !string.IsNullOrEmpty(v));

                // Update cache
                var cacheDir = Path.GetDirectoryName(cacheFile)!;
                Directory.CreateDirectory(cacheDir);
                await File.WriteAllTextAsync(cacheFile, JsonSerializer.Serialize(
                    new VersionCheckCache { LastCheck = DateTime.UtcNow, LatestVersion = latestVersion },
                    new JsonSerializerOptions { WriteIndented = true }));
            }

            if (latestVersion == null) return;

            var current = Assembly.GetEntryAssembly()
                ?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()
                ?.InformationalVersion ?? "0.0.0";

            // Strip build metadata (e.g. "2.0.0+abc123" → "2.0.0")
            current = current.Split('+')[0];

            if (Version.TryParse(latestVersion, out var latest) &&
                Version.TryParse(current, out var currentVer) &&
                latest > currentVer)
            {
                Console.WriteLine();
                AnsiConsole.Write(new Panel(
                    $"[yellow]A new version of yp is available:[/] [bold green]{latestVersion}[/]  [dim](current: {current})[/]\n" +
                    "Update with: [cyan]dotnet tool update -g YitPush[/]")
                    .BorderColor(Color.Yellow)
                    .Padding(1, 0));
            }
        }
        catch { /* Silent — version check is non-critical */ }
    }
}
