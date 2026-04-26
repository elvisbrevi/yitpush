using Spectre.Console;

namespace YitPush;

partial class Program
{
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
            var (oUrl, project, projectId) = setup.Value;
            var workItemId = AnsiConsole.Prompt(
                new TextPrompt<string>("Work item ID:"));
            return await AddLinkToRepo(oUrl, projectId, workItemId);
        }

        if (args.Length < 2)
        {
            ShowAzureDevOpsHelp();
            return 1;
        }

        var resource = args[0];
        var action = args[1];

        // Flag parsing
        string? description = null;
        string? effort = null;
        string? repo = null;
        string? branch = null;
        string? remaining = null;
        string? state = null;
        string? comment = null;
        string? effortReal = null;

        for (int i = 0; i < args.Length; i++)
        {
            if ((args[i] == "--description" || args[i] == "-d") && i + 1 < args.Length)
            {
                description = args[i + 1];
            }
            if ((args[i] == "--effort" || args[i] == "-e") && i + 1 < args.Length)
            {
                effort = args[i + 1];
            }
            if ((args[i] == "--effort-real" || args[i] == "-er") && i + 1 < args.Length)
            {
                effortReal = args[i + 1];
            }
            if (args[i] == "--repo" && i + 1 < args.Length)
            {
                repo = args[i + 1];
            }
            if (args[i] == "--branch" && i + 1 < args.Length)
            {
                branch = args[i + 1];
            }
            if ((args[i] == "--remaining" || args[i] == "-r") && i + 1 < args.Length)
            {
                remaining = args[i + 1];
            }
            if ((args[i] == "--state" || args[i] == "-s") && i + 1 < args.Length)
            {
                state = args[i + 1];
            }
            if ((args[i] == "--comment" || args[i] == "-c") && i + 1 < args.Length)
            {
                comment = args[i + 1];
            }
        }

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
            // Quick mode: yp azure-devops hu task <org> <project> <hu-id> [--description "desc"] [--effort "5"]
            if (args.Length >= 5 && !args[2].StartsWith("-"))
            {
                var org = args[2];
                var proj = args[3];
                var huId = args[4];
                var orgUrl = $"https://dev.azure.com/{org}";
                return await CreateTasksDirectForHU(orgUrl, proj, huId, description, effort);
            }
            var result = await ListAzureUserStories(description, effort);
            return result == BackToMenu ? 0 : result;
        }
        else if (resource == "hu" && action == "show")
        {
            // Quick mode: yp azure-devops hu show <org> <hu-id>
            if (args.Length >= 4)
            {
                var orgUrl = $"https://dev.azure.com/{args[2]}";
                return await ShowWorkItemDetails(orgUrl, args[3]);
            }

            var result = await ListAzureUserStoriesForTaskList(); // Reuse list for selecting HU to show
            return result == BackToMenu ? 0 : result;
        }
        else if (resource == "task" && action == "show")
        {
            // Quick mode: yp azure-devops task show <org> <id>
            if (args.Length >= 4)
            {
                var orgUrl = $"https://dev.azure.com/{args[2]}";
                return await ShowWorkItemDetails(orgUrl, args[3]);
            }

            // For tasks, we usually go through HUs
            var result = await ListAzureUserStoriesForTaskList();
            return result == BackToMenu ? 0 : result;
        }
        else if (resource == "hu" && action == "link")
        {
            // Quick mode: yp azure-devops hu link <org> <project> <hu-id> --repo <repo> --branch <branch>
            if (args.Length >= 5 && !args[2].StartsWith("-"))
            {
                var org = args[2];
                var proj = args[3];
                var huId = args[4];
                var orgUrl = $"https://dev.azure.com/{org}";
                return await AddLinkToRepo(orgUrl, proj, huId, repo, branch);
            }

            var result = await ListAzureUserStoriesForLinking();
            return result == BackToMenu ? 0 : result;
        }
        else if (resource == "hu" && action == "list")
        {
            // Quick mode: yp azure-devops hu list <org> <project> <hu-id>
            if (args.Length >= 5)
            {
                var org = args[2];
                var proj = args[3];
                var huId = args[4];
                var orgUrl = $"https://dev.azure.com/{org}";
                return await ListTasksForHU(orgUrl, proj, proj, huId);
            }
            // Interactive mode: select HU first, then list tasks
            var result = await ListAzureUserStoriesForTaskList();
            return result == BackToMenu ? 0 : result;
        }
        else if ((resource == "hu" || resource == "task" || resource == "wi") && action == "update")
        {
            // Quick mode: yp azure-devops task update <org> <id> [--effort "val"] [--remaining "val"] [--state "val"] [--comment "val"]
            if (args.Length >= 4 && !args[2].StartsWith("-"))
            {
                var orgForUpdate = args[2];
                var idForUpdate = args[3];
                var orgUrlForUpdate = $"https://dev.azure.com/{orgForUpdate}";
                // Ensure all flags (effort, remaining, state, comment, effortReal) are passed from the outer scope parsing
                return await UpdateWorkItem(orgUrlForUpdate, idForUpdate, effort, remaining, state, comment, effortReal);
            }

            // Interactive mode: select work item first
            var setup = await EnsureAzureDevOpsSetup();
            if (setup == null) return 1;
            var (orgUrlS, projectName, projectId) = setup.Value;

            var idPrompt = AnsiConsole.Prompt(new TextPrompt<string>("Work item ID:"));
            return await UpdateWorkItemInteractive(orgUrlS, idPrompt);
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
                    var (oUrl, proj, projId) = setup.Value;
                    var workItemId = AnsiConsole.Prompt(
                        new TextPrompt<string>("Work item ID:"));
                    result = await AddLinkToRepo(oUrl, projId, workItemId);
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

    private static void ShowAzureDevOpsHelp()
    {
        AnsiConsole.MarkupLine("[bold]Usage:[/] yp azure-devops [bold]<subcommand>[/]");
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
        table.AddRow("hu task <org> <proj> <hu-id> [[--description \"...\"]] [[--effort \"...\"]]", "Create tasks (skip menus)");
        table.AddRow("hu show", "Show User Story details");
        table.AddRow("hu show <org> <hu-id>", "Show details (skip menus)");
        table.AddRow("hu list", "List tasks of a User Story");
        table.AddRow("hu list <org> <proj> <hu-id>", "List tasks (skip menus)");
        table.AddRow("hu link", "Link a repository branch to a User Story");
        table.AddRow("hu link <org> <proj> <hu-id> --repo <repo> --branch <branch>", "Link branch (skip menus)");
        table.AddRow("task show", "Show task details");
        table.AddRow("task show <org> <id>", "Show details (skip menus)");
        table.AddRow("task update", "Update effort, remaining, state or comment (alias: hu update, wi update)");
        table.AddRow("task update <org> <id> [[--effort|-e <e>]] [[--effort-real|-er <er>]] [[--remaining|-r <r>]] [[--state|-s <s>]] [[--comment|-c <c>]]", "Update directly");
        table.AddRow("link", "Add link (branch/commit/PR) to work item");
        table.AddRow("link <org> <proj> <wi-id>", "Add link (skip menus)");

        AnsiConsole.Write(table);

        AnsiConsole.MarkupLine("\n[dim]Short flags for hu task: --description|-d, --effort|-e[/]");

        AnsiConsole.MarkupLine("\n[bold]Examples:[/]");
        AnsiConsole.MarkupLine("  yp azure-devops hu show MyOrg 12345           [dim]# Show HU info[/]");
        AnsiConsole.MarkupLine("  yp azure-devops task show MyOrg 67890         [dim]# Show Task info[/]");
        AnsiConsole.MarkupLine("  yp azure-devops task update MyOrg 67890 --effort \"8\" --state \"Doing\"  [dim]# Update task[/]");
        AnsiConsole.MarkupLine("  yp azure-devops task update MyOrg 67890 --comment \"Fixed the bug\"     [dim]# Add comment[/]");
        AnsiConsole.MarkupLine("  yp azure-devops hu task MyOrg MyProj 123 --effort \"8\"  [dim]# Quick task with effort[/]");
        AnsiConsole.MarkupLine("  yp azure-devops hu link MyOrg MyProj 123 --repo Repo --branch main  [dim]# Quick link[/]");
        AnsiConsole.MarkupLine("  yp azure-devops hu list MyOrg MyProj 123      [dim]# List tasks of HU[/]");
        AnsiConsole.MarkupLine("  yp azure-devops link MyOrg MyProj 123         [dim]# Add link to work item[/]");
        Console.WriteLine();
    }
}
