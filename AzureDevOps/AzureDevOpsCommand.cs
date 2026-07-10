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

        if (args[0] == "resolve-field")
        {
            if (args.Length < 5)
            {
                AnsiConsole.MarkupLine("[red]Usage: yp azure-devops resolve-field <org> <project> <workItemType> <displayName>[/]");
                return 1;
            }
            return await ResolveFieldCli(args[1], args[2], args[3], args[4]);
        }

        if (args[0] == "refresh-fields")
        {
            if (args.Length < 4)
            {
                AnsiConsole.MarkupLine("[red]Usage: yp azure-devops refresh-fields <org> <project> <workItemType>[/]");
                return 1;
            }
            return await RefreshFieldsCli(args[1], args[2], args[3]);
        }

        if (args.Length < 2)
        {
            ShowAzureDevOpsHelp();
            return 1;
        }

        var resource = args[0];
        var action = args[1];

        var flags = AzureDevOpsFlagParser.Parse(args);

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
            // Quick mode: yp azure-devops hu task <org> <project> <hu-id> [--title "..."] [--description "desc"] [--effort "5"] [--trio] [--no-link]
            if (args.Length >= 5 && !args[2].StartsWith("-"))
            {
                var org = args[2];
                var proj = args[3];
                var huId = args[4];
                var orgUrl = $"https://dev.azure.com/{org}";

                try
                {
                    HuTaskPlan.ComputePlan(flags.Title, flags.TaskTitles, flags.Trio);
                }
                catch (ArgumentException ex)
                {
                    AnsiConsole.MarkupLine($"[red]❌ {Markup.Escape(ex.Message)}[/]");
                    return 5;
                }

                return await CreateTasksDirectForHU(
                    orgUrl, proj, huId,
                    flags.Title, flags.Description, flags.Effort, flags.TaskTitles, flags.Trio,
                    flags.NoLink, flags.Repo, flags.Branch);
            }
            var result = await ListAzureUserStories(flags.Description, flags.Effort);
            return result == BackToMenu ? 0 : result;
        }
        else if (resource == "hu" && action == "show")
        {
            // Quick mode: yp azure-devops hu show <org> <hu-id>
            if (args.Length >= 4)
            {
                var orgUrl = $"https://dev.azure.com/{args[2]}";
                return await ShowWorkItemDetails(orgUrl, args[3], json: flags.Json);
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
                return await ShowWorkItemDetails(orgUrl, args[3], json: flags.Json);
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
                return await AddLinkToRepo(orgUrl, proj, huId, flags.Repo, flags.Branch);
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
                return await ListTasksForHU(orgUrl, proj, proj, huId, json: flags.Json);
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
                return await UpdateWorkItem(
                    orgUrlForUpdate, idForUpdate,
                    flags.Effort, flags.Remaining, flags.State, flags.Comment, flags.EffortReal,
                    flags.Title, flags.Description, flags.Evidence,
                    flags.ExtraFields, flags.History);
            }

            // Interactive mode: select work item first
            var setup = await EnsureAzureDevOpsSetup();
            if (setup == null) return 1;
            var (orgUrlS, projectName, projectId) = setup.Value;

            var idPrompt = AnsiConsole.Prompt(new TextPrompt<string>("Work item ID:"));
            return await UpdateWorkItemInteractive(orgUrlS, idPrompt);
        }
        else if (resource == "task" && action == "delete")
        {
            // Quick mode: yp azure-devops task delete <org> <id> [--yes] [--json]
            if (args.Length >= 4 && !args[2].StartsWith("-"))
            {
                return await TaskDeleteCli(args[2], args[3], flags.Yes, flags.Json);
            }

            AnsiConsole.MarkupLine("[red]Usage: yp azure-devops task delete <org> <id> [--yes] [--json][/]");
            return 1;
        }
        else if (resource == "task" && action == "attach")
        {
            // Quick mode: yp azure-devops task attach <org> <proj> <id> <file-path> [--comment "..."] [--json]
            if (args.Length >= 6 && !args[2].StartsWith("-"))
            {
                return await TaskAttachCli(args[2], args[3], args[4], args[5], flags.Comment, flags.Json);
            }

            AnsiConsole.MarkupLine("[red]Usage: yp azure-devops task attach <org> <project> <id> <file-path> [--comment \"...\"] [--json][/]");
            return 1;
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
                    .EnableSearch()
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
        table.AddRow("hu task <org> <proj> <hu-id> [[--title <t>]] [[--description|-d|-D \"...\"]] [[--effort|-e \"...\"]] [[--trio]] [[--task-titles|-t \"A, B, C\"]] [[--no-link|-n]] [[--repo <repo> --branch <branch>]]", "Create tasks (skip menus) — single task by default, --trio for legacy trio, --task-titles for custom list");
        table.AddRow("hu show", "Show User Story details");
        table.AddRow("hu show <org> <hu-id> --json", "Show details (skip menus) as a flat JSON object suitable for jq");
        table.AddRow("hu list", "List tasks of a User Story");
        table.AddRow("hu list <org> <proj> <hu-id> --json", "List tasks (skip menus) as JSON for jq '.value | length'");
        table.AddRow("hu link", "Link a repository branch to a User Story");
        table.AddRow("hu link <org> <proj> <hu-id> --repo <repo> --branch <branch>", "Link branch (skip menus)");
        table.AddRow("task show", "Show task details");
        table.AddRow("task show <org> <id> --json", "Show details (skip menus) as a flat JSON object");
        table.AddRow("task update", "Update title, description, evidence, effort, remaining, state, comment or history (alias: hu update, wi update)");
        table.AddRow("task update <org> <id> [[--title <t>]] [[--description|-D <d>]] [[--evidence <e>]] [[--field <RefName=val>]] [[--effort|-e <e>]] [[--effort-real|-er <er>]] [[--remaining|-r <r>]] [[--state|-s <s>]] [[--comment|-c <c>]] [[--history <h>]]", "Update directly");
        table.AddRow("task delete <org> <id> [[--yes|-y]] [[--json]]", "Move work item to the recycle bin (prompts by default; pass --yes in CI)");
        table.AddRow("task attach <org> <project> <id> <file-path> [[--comment|-c \"...\"]] [[--json]]", "Upload a local file as an AttachedFile relation on the work item");
        table.AddRow("link", "Add link (branch/commit/PR) to work item");
        table.AddRow("link <org> <proj> <wi-id>", "Add link (skip menus)");
        table.AddRow("resolve-field <org> <proj> <type> <displayName>", "Print the refname for a display name; uses the 24h cache");
        table.AddRow("refresh-fields <org> <proj> <type>", "Invalidate the cache for (org, project, workItemType) and re-warm it");

        AnsiConsole.Write(table);

        AnsiConsole.MarkupLine("\n[dim]Short flags for hu task: --title, --description|-d|-D, --effort|-e, --trio, --task-titles|-t, --no-link|-n[/]");
        AnsiConsole.MarkupLine("[dim]hu task default: 1 task. Title precedence: --title > --task-titles[0] > 'Desarrollo'. --trio creates the legacy trio (opt-in). --trio and --task-titles are mutually exclusive.[/]");
        AnsiConsole.MarkupLine("[dim]task update: --comment posts to Discussion; --history writes the legacy History field; --evidence resolves the 'Evidencias de finalización' field by name[/]");
        AnsiConsole.MarkupLine("[dim]--json on hu/task show and hu list emits a flat JSON shape (no ANSI escapes) so it can be piped to jq[/]");

        AnsiConsole.MarkupLine("\n[bold]Examples:[/]");
        AnsiConsole.MarkupLine("  yp azure-devops hu show MyOrg 12345           [dim]# Show HU info[/]");
        AnsiConsole.MarkupLine("  yp azure-devops hu show MyOrg 12345 --json    [dim]# Show HU info as JSON for jq[/]");
        AnsiConsole.MarkupLine("  yp azure-devops task show MyOrg 67890         [dim]# Show Task info[/]");
        AnsiConsole.MarkupLine("  yp azure-devops task show MyOrg 67890 --json  [dim]# Show Task info as JSON for jq[/]");
        AnsiConsole.MarkupLine("  yp azure-devops task update MyOrg 67890 --effort \"8\" --state \"Doing\"  [dim]# Update task[/]");
        AnsiConsole.MarkupLine("  yp azure-devops task update MyOrg 67890 --comment \"Fixed the bug\"     [dim]# Add comment[/]");
        AnsiConsole.MarkupLine("  yp azure-devops hu task MyOrg MyProj 123 --title \"WCF: op SOAP\" -d \"...\" -e 8   [dim]# 1 task with explicit title (v2.3.0 default)[/]");
        AnsiConsole.MarkupLine("  yp azure-devops hu task MyOrg MyProj 123 -d \"...\" -e 8 -n                       [dim]# 1 task titled 'Desarrollo', no linking[/]");
        AnsiConsole.MarkupLine("  yp azure-devops hu task MyOrg MyProj 123 --trio -d \"...\" -e 8                  [dim]# Legacy trio (opt-in)[/]");
        AnsiConsole.MarkupLine("  yp azure-devops hu task MyOrg MyProj 123 --effort \"8\" -t \"Desarrollo, Pruebas\" -n  [dim]# Custom multi-task list[/]");
        AnsiConsole.MarkupLine("  yp azure-devops hu task MyOrg MyProj 123 --effort \"8\" --repo MyRepo --branch feature/123  [dim]# Auto-link branch to tasks[/]");
        AnsiConsole.MarkupLine("  yp azure-devops hu link MyOrg MyProj 123 --repo Repo --branch main  [dim]# Quick link[/]");
        AnsiConsole.MarkupLine("  yp azure-devops hu list MyOrg MyProj 123      [dim]# List tasks of HU[/]");
        AnsiConsole.MarkupLine("  yp azure-devops hu list MyOrg MyProj 123 --json | jq '.value | length'   [dim]# Count child tasks[/]");
        AnsiConsole.MarkupLine("  yp azure-devops link MyOrg MyProj 123         [dim]# Add link to work item[/]");
        AnsiConsole.MarkupLine("  yp azure-devops resolve-field MyOrg MyProj Task \"Esfuerzo Real\"   [dim]# Print refname (cached for 24h)[/]");
        AnsiConsole.MarkupLine("  yp azure-devops refresh-fields MyOrg MyProj Task                  [dim]# Invalidate + re-warm cache[/]");
        AnsiConsole.MarkupLine("  yp azure-devops task delete MyOrg 22428 --yes                     [dim]# Move a work item to the recycle bin (skip prompt in CI)[/]");
        AnsiConsole.MarkupLine("  yp azure-devops task attach MyOrg MyProj 22427 ./screenshot.png   [dim]# Upload a local file as an AttachedFile[/]");
        AnsiConsole.MarkupLine("  yp azure-devops task attach MyOrg MyProj 22427 ./screenshot.png -c 'Curl folio 3535303'  [dim]# Attach with comment[/]");
        Console.WriteLine();
    }
}
