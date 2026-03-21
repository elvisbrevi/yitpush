using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.RegularExpressions;
using Spectre.Console;

namespace YitPush;

partial class Program
{
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

    private static async Task<(string OrgUrl, string ProjectName, string ProjectId)?> EnsureAzureDevOpsSetup()
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

            // Projects are already sorted by ID DESC in FetchAzureProjects
            var projectChoices = new List<string>(projects.Select(p => p.Name)) { BackOption };
            var selectedProjectName = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title("Project:")
                    .PageSize(10)
                    .HighlightStyle(new Style(Color.Cyan1))
                    .AddChoices(projectChoices));

            if (selectedProjectName == BackOption) continue;

            var project = projects.First(p => p.Name == selectedProjectName);
            return (orgUrl, project.Name, project.Id);
        }
    }

    private static async Task<string?> CreateAzureDevOpsRepo()
    {

        var setup = await EnsureAzureDevOpsSetup();
        if (setup == null) return null;

        var (orgUrl, selectedProject, selectedProjectId) = setup.Value;

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
                $"repos show --repository \"{repoName}\" --organization {orgUrl} --project \"{selectedProjectId}\" --output json");

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
                    $"repos create --name \"{repoName}\" --organization {orgUrl} --project \"{selectedProjectId}\" --output json");

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

        var (orgUrl, selectedProject, selectedProjectId) = setup.Value;

        var repos = await FetchAzureRepos(orgUrl, selectedProjectId);

        if (repos.Count == 0)
        {
            AnsiConsole.MarkupLine("[red]❌ No repositories found in this project.[/]");
            return 1;
        }

        // Repos are already sorted by ID DESC in FetchAzureRepos
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

    private static async Task<int> ListAzureUserStories(string? description = null, string? effort = null)
    {

        var setup = await EnsureAzureDevOpsSetup();
        if (setup == null) return 1;

        var (orgUrl, projectName, projectId) = setup.Value;

        var hus = await FetchAzureUserStories(orgUrl, projectName);

        if (hus.Count == 0)
        {
            AnsiConsole.MarkupLine("[yellow]No User Stories found.[/]");
            return 0;
        }

        // Final sort by ID DESC
        var sortedHus = hus.OrderByDescending(h => int.TryParse(h.Id, out var id) ? id : 0).ToList();

        var maxIdLen = sortedHus.Max(h => h.Id.Length);
        var displayMap = new Dictionary<string, (string Id, string Title, string State, string Date, string Area, string Iteration)>();
        var displayList = new List<string>();

        foreach (var hu in sortedHus)
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
                    .AddChoices("Show details", "Create standard tasks", "Create branch for this HU", "Mark as In Progress", "Add link to repo", BackOption));

            if (action == BackOption) continue;

            if (action == "Show details")
            {
                await ShowWorkItemDetails(orgUrl, selectedHu.Id);
            }
            else if (action == "Create standard tasks")
            {
                await CreateTasksForUserStory(orgUrl, projectName, selectedHu.Id, selectedHu.Area, selectedHu.Iteration, description, effort);
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
                await AddLinkToRepo(orgUrl, projectId, selectedHu.Id);
            }
        }
    }

    private static async Task<int> CreateTasksForUserStory(string orgUrl, string project, string huId, string areaPath, string iterationPath, string? fixedDescription = null, string? fixedEffort = null)
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

        // Ask for estimated effort if not fixed
        var effortHours = fixedEffort;
        if (string.IsNullOrEmpty(effortHours))
        {
            effortHours = AnsiConsole.Prompt(
                new TextPrompt<string>("Estimated effort in hours (default: 1):")
                    .DefaultValue("1")
                    .AllowEmpty());
        }

        if (string.IsNullOrWhiteSpace(effortHours)) effortHours = "1";

        var createdTaskIds = new List<string>();

        foreach (var title in titles)
        {
            var description = fixedDescription;
            if (description == null)
            {
                description = AnsiConsole.Prompt(
                    new TextPrompt<string>($"Description for [cyan]{Markup.Escape(title)}[/] (optional):")
                        .AllowEmpty());
            }

            AnsiConsole.Markup($"[dim]{Markup.Escape(title)}...[/] ");

            // Initial fields based on SAG project requirements
            var fields = $"\"{AzFieldRemainingWork}=0\" \"{AzFieldEffortHH}={effortHours}\" \"Custom.Mes={currentMonth}\"{assignedField}";

            if (!string.IsNullOrEmpty(description))
            {
                // Escape description for AZ CLI fields - it needs careful quoting
                // Azure CLI fields take "Field=Value" and we already wrap the whole thing in quotes
                var escapedDescription = description.Replace("\"", "\\\"");
                fields += $" \"System.Description={escapedDescription}\"";
            }

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
                    createdTaskIds.Add(taskId);
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

        if (createdTaskIds.Count > 0 && AnsiConsole.Confirm("Would you like to link a [cyan]branch[/] to these tasks?"))
        {
            foreach (var taskId in createdTaskIds)
            {
                AnsiConsole.MarkupLine($"\n[bold]Linking to task #{taskId}...[/]");
                await AddLinkToRepo(orgUrl, project, taskId);
            }
        }

        return 0;
    }

    private static async Task<int> CreateTasksDirectForHU(string orgUrl, string project, string huId, string? description = null, string? effort = null)
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

        return await CreateTasksForUserStory(orgUrl, project, huId, areaPath, iterationPath, description, effort);
    }

    private static async Task<int> ListTasksForHU(string orgUrl, string projectName, string projectId, string huId)
    {
        string finalProjectId = projectId;
        // If projectId is not a UUID, fetch it
        if (!finalProjectId.Contains("-"))
        {
            var projects = await FetchAzureProjects(orgUrl);
            var p = projects.FirstOrDefault(p => string.Equals(p.Name, projectName, StringComparison.OrdinalIgnoreCase));
            if (p.Id != null) finalProjectId = p.Id;
        }

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
            string taskTitle = id, state = "";
            if (detailJson != null)
            {
                try
                {
                    using var detailDoc = JsonDocument.Parse(detailJson);
                    var fields = detailDoc.RootElement.GetProperty("fields");
                    taskTitle = fields.TryGetProperty("System.Title", out var tp) ? tp.GetString() ?? id : id;
                    state = fields.TryGetProperty("System.State", out var sp) ? sp.GetString() ?? "" : "";
                }
                catch { }
            }
            tasks.Add((id, taskTitle, state));
        }

        if (tasks.Count == 0)
        {
            AnsiConsole.MarkupLine($"[yellow]No tasks found for HU {huId}.[/]");
            return 0;
        }

        // Sort tasks by ID DESC
        var sortedTasks = tasks.OrderByDescending(t => int.TryParse(t.Id, out var id) ? id : 0).ToList();

        foreach (var t in sortedTasks)
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
            var taskChoices = sortedTasks.Select(t => $"{t.Id} - {t.Title}").ToList();
            taskChoices.Add(BackOption);
            var selected = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title("Select task to manage:")
                    .AddChoices(taskChoices));

            if (selected == BackOption) return 0;

            var taskId = selected.Split(" - ")[0].Trim();

            var action = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title("What would you like to do?")
                    .AddChoices("Show details", "Update work item", "Add link to repo", BackOption));

            if (action == BackOption) continue;

            if (action == "Show details")
            {
                await ShowWorkItemDetails(orgUrl, taskId);
            }
            else if (action == "Update work item")
            {
                await UpdateWorkItemInteractive(orgUrl, taskId);
            }
            else if (action == "Add link to repo")
            {
                await AddLinkToRepo(orgUrl, projectId, taskId);
            }
        }
    }


    private static async Task<int> ListAzureUserStoriesForTaskList()
    {
        var setup = await EnsureAzureDevOpsSetup();
        if (setup == null) return 1;

        var (orgUrl, projectName, projectId) = setup.Value;
        var hus = await FetchAzureUserStories(orgUrl, projectName);

        if (hus.Count == 0)
        {
            AnsiConsole.MarkupLine("[yellow]No User Stories found.[/]");
            return 0;
        }

        // Final sort by ID DESC
        var sortedHus = hus.OrderByDescending(h => int.TryParse(h.Id, out var id) ? id : 0).ToList();

        var maxIdLen = sortedHus.Max(h => h.Id.Length);
        var displayMap = new Dictionary<string, string>();
        var displayList = new List<string>();

        foreach (var hu in sortedHus)
        {
            var display = $"[grey]{hu.Date}[/] [cyan]{hu.Id.PadRight(maxIdLen + 1)}[/] {Markup.Escape(hu.Title)} [dim]({Markup.Escape(hu.State)})[/]";
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
            await ListTasksForHU(orgUrl, projectName, projectId, huId);
        }
    }

    private static async Task<int> ListAzureUserStoriesForLinking()
    {
        var setup = await EnsureAzureDevOpsSetup();
        if (setup == null) return 1;
        var (orgUrl, projectName, projectId) = setup.Value;

        var hus = await FetchAzureUserStories(orgUrl, projectName);
        if (hus.Count == 0)
        {
            AnsiConsole.MarkupLine("[yellow]No User Stories found.[/]");
            return 0;
        }

        // Final sort by ID DESC
        var sortedHus = hus.OrderByDescending(h => int.TryParse(h.Id, out var id) ? id : 0).ToList();

        var maxIdLen = sortedHus.Max(h => h.Id.Length);
        var displayMap = new Dictionary<string, string>();
        var choices = new List<string>();

        foreach (var hu in sortedHus)
        {
            var display = $"[grey]{hu.Date}[/] [cyan]{hu.Id.PadRight(maxIdLen + 1)}[/] {Markup.Escape(hu.Title)} [dim]({Markup.Escape(hu.State)})[/]";
            displayMap[display] = hu.Id;
            choices.Add(display);
        }
        choices.Add(BackOption);

        while (true)
        {
            var selected = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title("Select [cyan]User Story[/] to link:")
                    .PageSize(15)
                    .HighlightStyle(new Style(Color.Cyan1))
                    .AddChoices(choices));

            if (selected == BackOption) return BackToMenu;

            var huId = displayMap[selected];
            await AddLinkToRepo(orgUrl, projectId, huId);
        }
    }

    private static Dictionary<string, string>? _relationTypeCache = null;
    private static async Task<string> ResolveArtifactRelationType(string orgUrl, string preferredType)
    {
        if (_relationTypeCache == null)
        {
            _relationTypeCache = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var json = await RunAzCapture($"boards work-item relation list-type --organization {orgUrl} --output json");
            if (json != null)
            {
                try
                {
                    using var doc = JsonDocument.Parse(json);
                    foreach (var item in doc.RootElement.EnumerateArray())
                    {
                        var name = item.TryGetProperty("name", out var n) ? n.GetString() : null;
                        var refName = item.TryGetProperty("referenceName", out var r) ? r.GetString() : null;

                        if (name != null && refName != null)
                        {
                            // Store mapping: ReferenceName -> Name (the one CLI expects)
                            if (!_relationTypeCache.ContainsKey(refName)) _relationTypeCache[refName] = name;
                            if (!_relationTypeCache.ContainsKey(name)) _relationTypeCache[name] = name;
                        }
                    }
                }
                catch { }
            }
        }

        bool isArtifactHint = preferredType is "Branch" or "Commit" or "Pull Request";

        if (isArtifactHint)
        {
            // For technical links, the standard reference name is "ArtifactLink"
            if (_relationTypeCache.TryGetValue("ArtifactLink", out var resolved)) return resolved;

            // Try fuzzy match for anything containing "Artifact"
            var artifactKey = _relationTypeCache.Keys.FirstOrDefault(k => k.Contains("Artifact", StringComparison.OrdinalIgnoreCase));
            if (artifactKey != null) return _relationTypeCache[artifactKey];

            return "Artifact Link"; // System default name for technical links
        }

        if (_relationTypeCache.TryGetValue(preferredType, out var res)) return res;

        return preferredType;
    }

    private static async Task<int> AddLinkToRepo(string orgUrl, string projectHint, string workItemId, string? fixedRepo = null, string? fixedBranch = null)
    {
        string finalProjectId = projectHint;
        string finalProjectName = projectHint;

        var projects = await FetchAzureProjects(orgUrl);
        var p = projects.FirstOrDefault(pr => string.Equals(pr.Name, projectHint, StringComparison.OrdinalIgnoreCase) || string.Equals(pr.Id, projectHint, StringComparison.OrdinalIgnoreCase));
        if (p.Name != null)
        {
            finalProjectId = p.Id;
            finalProjectName = p.Name;
        }

        // Fetch repositories
        var repos = await FetchAzureRepos(orgUrl, finalProjectId);
        if (repos.Count == 0)
        {
            AnsiConsole.MarkupLine("[red]❌ No repositories found in this project.[/]");
            return 1;
        }

        (string Name, string RemoteUrl, string Id)? selectedRepo = null;
        if (!string.IsNullOrEmpty(fixedRepo))
        {
            selectedRepo = repos.FirstOrDefault(r => string.Equals(r.Name, fixedRepo, StringComparison.OrdinalIgnoreCase));
            if (selectedRepo == null)
            {
                AnsiConsole.MarkupLine($"[red]❌ Repository '{fixedRepo}' not found.[/]");
                return 1;
            }
        }
        else
        {
            var repoChoices = new List<string>(repos.Select(r => r.Name)) { BackOption };
            var selectedRepoName = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title("📋 Select [green]repository[/]:\n[dim](Select ← Back to return)[/]")
                    .PageSize(15)
                    .HighlightStyle(new Style(Color.Cyan1))
                    .AddChoices(repoChoices));

            if (selectedRepoName == BackOption) return 0;
            selectedRepo = repos.First(r => r.Name == selectedRepoName);
        }

        string artifactUrl = "";
        string webUrl = "";
        string relationTypeHint = "Hyperlink";
        string branchToLink = fixedBranch ?? "";

        if (!string.IsNullOrEmpty(fixedBranch))
        {
            relationTypeHint = "Branch";
            artifactUrl = $"vstfs:///Git/Ref/{finalProjectId}/{selectedRepo.Value.Id}/GB{Uri.EscapeDataString(fixedBranch)}";
            webUrl = $"{orgUrl}/{Uri.EscapeDataString(finalProjectName)}/_git/{Uri.EscapeDataString(selectedRepo.Value.Name)}?version=GB{Uri.EscapeDataString(fixedBranch)}";
        }
        else
        {
            var linkType = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title("Link type:")
                    .AddChoices("Branch", "Commit", "Pull Request", BackOption));

            if (linkType == BackOption) return 0;

            if (linkType == "Branch")
            {
                relationTypeHint = "Branch";
                var branchesJson = await RunAzCapture($"repos ref list --repository \"{selectedRepo.Value.Name}\" --organization {orgUrl} --project \"{finalProjectId}\" --output json");
                var branches = new List<string>();
                if (branchesJson != null)
                {
                    try
                    {
                        using var doc = JsonDocument.Parse(branchesJson);
                        JsonElement items = doc.RootElement.ValueKind == JsonValueKind.Array ? doc.RootElement : (doc.RootElement.TryGetProperty("value", out var v) ? v : default);
                        if (items.ValueKind == JsonValueKind.Array)
                        {
                            foreach (var b in items.EnumerateArray())
                            {
                                if (b.TryGetProperty("name", out var n))
                                {
                                    var name = n.GetString();
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
                    var selectedBranch = AnsiConsole.Prompt(new SelectionPrompt<string>().Title("Select [green]branch[/]:").AddChoices(branchChoices));
                    if (selectedBranch == BackOption) return 0;
                    branchToLink = selectedBranch == "Enter custom branch name" ? AnsiConsole.Prompt(new TextPrompt<string>("Enter branch name:").DefaultValue("main")) : selectedBranch;
                }
                else
                {
                    branchToLink = AnsiConsole.Prompt(new TextPrompt<string>("Enter branch name:").DefaultValue("main"));
                }
                artifactUrl = $"vstfs:///Git/Ref/{finalProjectId}/{selectedRepo.Value.Id}/GB{Uri.EscapeDataString(branchToLink)}";
                webUrl = $"{orgUrl}/{Uri.EscapeDataString(finalProjectName)}/_git/{Uri.EscapeDataString(selectedRepo.Value.Name)}?version=GB{Uri.EscapeDataString(branchToLink)}";
            }
            else if (linkType == "Commit")
            {
                relationTypeHint = "Commit";
                var commitHash = AnsiConsole.Prompt(new TextPrompt<string>("Enter commit hash:").Validate(h => !string.IsNullOrWhiteSpace(h)));
                artifactUrl = $"vstfs:///Git/Commit/{finalProjectId}/{selectedRepo.Value.Id}/{commitHash}";
                webUrl = $"{orgUrl}/{Uri.EscapeDataString(finalProjectName)}/_git/{Uri.EscapeDataString(selectedRepo.Value.Name)}/commit/{commitHash}";
            }
            else if (linkType == "Pull Request")
            {
                relationTypeHint = "Pull Request";
                var prId = AnsiConsole.Prompt(new TextPrompt<string>("Enter PR ID:").Validate(id => int.TryParse(id, out _)));
                artifactUrl = $"vstfs:///Git/PullRequestId/{finalProjectId}/{selectedRepo.Value.Id}/{prId}";
                webUrl = $"{orgUrl}/{Uri.EscapeDataString(finalProjectName)}/_git/{Uri.EscapeDataString(selectedRepo.Value.Name)}/pullrequest/{prId}";
            }
        }

        if (string.IsNullOrEmpty(artifactUrl)) return 1;

        // Primary goal: Update the specific 'URL Commit' field in Details
        AnsiConsole.MarkupLine($"[dim]Updating 'URL Commit' field...[/]");
        var updateSuccess = await RunAzPassthrough(
            $"boards work-item update --id {workItemId} --organization {orgUrl} --fields \"Custom.URLCommit={webUrl}\" --output none");

        if (updateSuccess)
            AnsiConsole.MarkupLine($"[green]Field 'URL Commit' updated.[/]");
        else
            AnsiConsole.MarkupLine($"[yellow]Could not update 'URL Commit' field (it may not exist or name is different).[/]");

        // Secondary: Also add the technical/hyperlink relation for the Links tab
        var relationType = await ResolveArtifactRelationType(orgUrl, relationTypeHint);

        // Advanced: Use 'az rest' to add ArtifactLink with the required 'name' attribute
        AnsiConsole.MarkupLine($"[dim]Linking to Development section...[/]");

        // Use organization-level URI for work items (more robust)
        var restUri = string.Format("{0}/_apis/wit/workitems/{1}?api-version=6.0", orgUrl, workItemId);

        // Build JSON Patch body. Note: name attribute is REQUIRED for technical links to show in 'Development'
        var body = string.Format("[{{\"op\": \"add\", \"path\": \"/relations/-\", \"value\": {{\"rel\": \"ArtifactLink\", \"url\": \"{0}\", \"attributes\": {{\"name\": \"{1}\"}} }} }}]", artifactUrl, relationTypeHint);

        // For 'az rest', the body needs to be escaped for the shell
        var escapedBodyForShell = body.Replace("\"", "\\\"");

        var (restResult, restError) = await RunAzCaptureWithError(
            string.Format("rest --method patch --resource {2} --uri \"{0}\" --headers \"Content-Type=application/json-patch+json\" --body \"{1}\"", restUri, escapedBodyForShell, AzDevOpsResource));

        if (restResult != null)
        {
            AnsiConsole.MarkupLine($"[green]Link added to Development section.[/]");
        }
        else
        {
            AnsiConsole.MarkupLine($"[yellow]Technical link via API failed:[/] [dim]{Markup.Escape(restError?.Trim() ?? "Unknown API error")}[/]");
            AnsiConsole.MarkupLine($"[dim]Trying standard CLI fallback...[/]");

            // Fallback to standard CLI
            var (result, linkError) = await RunAzCaptureWithError(
                $"boards work-item relation add --id {workItemId} --relation-type \"{relationType}\" --target-url \"{artifactUrl}\" --organization {orgUrl} --comment \"{branchToLink}\" --output json");

            if (result != null)
                AnsiConsole.MarkupLine($"[green]Link added to Links tab.[/]");
            else
            {
                AnsiConsole.MarkupLine($"[yellow]Retrying with functional Web Hyperlink...[/]");
                var (retryResult, retryError) = await RunAzCaptureWithError(
                    $"boards work-item relation add --id {workItemId} --relation-type Hyperlink --target-url \"{webUrl}\" --organization {orgUrl} --output json");
                if (retryResult != null)
                    AnsiConsole.MarkupLine($"[green]Link added as functional Hyperlink.[/]");
                else
                    AnsiConsole.MarkupLine($"[red]Failed to add link.[/] [dim]{Markup.Escape(retryError?.Trim() ?? "")}[/]");
            }
        }

        return 0;
    }

    private static async Task<int> ListAzureVariableGroups()
    {

        var setup = await EnsureAzureDevOpsSetup();
        if (setup == null) return 1;

        var (orgUrl, projectName, projectId) = setup.Value;

        var json = await RunAzCapture(
            $"pipelines variable-group list --organization {orgUrl} --project \"{projectId}\" --output json");

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

        // Get user profile to obtain memberId
        var (profileJson, profileError) = await RunAzCaptureWithError(
            $"rest --method get --resource {AzDevOpsResource} --url https://app.vssps.visualstudio.com/_apis/profile/profiles/me?api-version=7.0");

        if (profileJson == null && IsAzureAuthError(profileError))
        {
            if (!await HandleAzureReLogin()) return organizations;

            (profileJson, profileError) = await RunAzCaptureWithError(
                $"rest --method get --resource {AzDevOpsResource} --url https://app.vssps.visualstudio.com/_apis/profile/profiles/me?api-version=7.0");
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
            $"rest --method get --resource {AzDevOpsResource} --url \"https://app.vssps.visualstudio.com/_apis/accounts?memberId={memberId}&api-version=7.0\"");

        if (orgsJson == null && IsAzureAuthError(orgsError))
        {
            if (!await HandleAzureReLogin()) return organizations;

            (orgsJson, orgsError) = await RunAzCaptureWithError(
                $"rest --method get --resource {AzDevOpsResource} --url \"https://app.vssps.visualstudio.com/_apis/accounts?memberId={memberId}&api-version=7.0\"");
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

    private static async Task<List<(string Name, string Id)>> FetchAzureProjects(string orgUrl)
    {
        var projects = new List<(string Name, string Id)>();
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
                var id = project.TryGetProperty("id", out var idProp) ? idProp.GetString() : null;
                if (name != null && id != null)
                {
                    projects.Add((name, id));
                }
            }
        }
        catch { }

        // Sort alphabetically by name
        projects.Sort((a, b) => StringComparer.OrdinalIgnoreCase.Compare(a.Name, b.Name));
        return projects;
    }

    private static async Task<List<(string Name, string RemoteUrl, string Id)>> FetchAzureRepos(string orgUrl, string project)
    {
        var repos = new List<(string Name, string RemoteUrl, string Id)>();
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
                var id = repo.TryGetProperty("id", out var idProp) ? idProp.GetString() : null;
                if (name != null && remoteUrl != null && id != null)
                    repos.Add((name, remoteUrl, id));
            }
        }
        catch { }

        // Sort by ID DESC as proxy for date
        repos.Sort((a, b) => StringComparer.OrdinalIgnoreCase.Compare(b.Id, a.Id));
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

        // Final sort by ID DESC
        return stories.OrderByDescending(s => int.TryParse(s.Id, out var id) ? id : 0).ToList();
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

    private static async Task<int> ShowWorkItemDetails(string orgUrl, string id)
    {
        var (json, error) = await RunAzCaptureWithError(
            $"boards work-item show --id {id} --expand relations --organization {orgUrl} --output json");

        if (json == null)
        {
            AnsiConsole.MarkupLine($"[red]❌ Work item {id} not found.[/]");
            if (!string.IsNullOrEmpty(error))
                AnsiConsole.MarkupLine($"[dim]{Markup.Escape(error)}[/]");
            return 1;
        }

        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            var fields = root.GetProperty("fields");

            var type = fields.TryGetProperty("System.WorkItemType", out var typeProp) ? typeProp.GetString() : "Work Item";
            var title = fields.TryGetProperty("System.Title", out var titleProp) ? titleProp.GetString() : "No Title";
            var state = fields.TryGetProperty("System.State", out var stateProp) ? stateProp.GetString() : "Unknown";
            var assignedTo = fields.TryGetProperty("System.AssignedTo", out var assignedProp) ?
                (assignedProp.ValueKind == JsonValueKind.Object && assignedProp.TryGetProperty("displayName", out var dp) ? dp.GetString() : assignedProp.ToString()) : "-";

            var createdDate = fields.TryGetProperty("System.CreatedDate", out var dateProp) ? dateProp.GetString() : "-";
            var area = fields.TryGetProperty("System.AreaPath", out var areaProp) ? areaProp.GetString() : "-";
            var iteration = fields.TryGetProperty("System.IterationPath", out var iterProp) ? iterProp.GetString() : "-";

            // Custom fields
            var effort = fields.TryGetProperty(AzFieldEffortHH, out var effortProp) ? effortProp.ToString() : "-";
            var effortReal = fields.TryGetProperty(AzFieldEffortRealHH, out var effortRealProp) ? effortRealProp.ToString() : "-";
            var mes = fields.TryGetProperty("Custom.Mes", out var mesProp) ? mesProp.GetString() : "-";
            var urlCommit = fields.TryGetProperty("Custom.URLCommit", out var urlProp) ? urlProp.GetString() : "-";
            var remaining = fields.TryGetProperty(AzFieldRemainingWork, out var remProp) ? remProp.ToString() : "-";
            var description = fields.TryGetProperty("System.Description", out var descProp) ? descProp.GetString() : "";

            // Display
            var panel = new Table()
                .Border(TableBorder.Rounded)
                .BorderColor(Color.Cyan1)
                .Title($"[bold cyan]{type} #{id}[/]")
                .AddColumn("Field")
                .AddColumn("Value");

            panel.AddRow("[bold]Title[/]", Markup.Escape(title ?? ""));
            panel.AddRow("[bold]State[/]", $"[yellow]{state}[/]");
            panel.AddRow("[bold]Assigned[/]", $"[green]{Markup.Escape(assignedTo ?? "")}[/]");
            panel.AddRow("[bold]Created[/]", createdDate ?? "");
            panel.AddRow("[bold]Area[/]", Markup.Escape(area ?? ""));
            panel.AddRow("[bold]Iteration[/]", Markup.Escape(iteration ?? ""));
            panel.AddRow("[bold]Effort (HH)[/]", effort ?? "");
            panel.AddRow("[bold]Esfuerzo Real (HH)[/]", effortReal ?? "");
            panel.AddRow("[bold]Remaining[/]", remaining ?? "");
            panel.AddRow("[bold]Month[/]", mes ?? "");
            panel.AddRow("[bold]URL Commit[/]", $"[blue]{Markup.Escape(urlCommit ?? "")}[/]");

            AnsiConsole.Write(panel);

            if (!string.IsNullOrEmpty(description))
            {
                AnsiConsole.MarkupLine("\n[bold cyan]Description:[/]");
                // Description often contains HTML from Azure Boards, let's do a simple strip or just show it
                var plainDesc = Regex.Replace(description, "<.*?>", string.Empty);
                AnsiConsole.MarkupLine(Markup.Escape(plainDesc.Trim()));
            }

            // Relations (Links and Children)
            if (root.TryGetProperty("relations", out var relations) && relations.ValueKind == JsonValueKind.Array)
            {
                var relTable = new Table()
                    .Border(TableBorder.None)
                    .AddColumn("[bold]Type[/]")
                    .AddColumn("[bold]Target[/]");

                bool hasRel = false;
                foreach (var rel in relations.EnumerateArray())
                {
                    var relType = rel.TryGetProperty("rel", out var rp) ? rp.GetString() : "";
                    var url = rel.TryGetProperty("url", out var up) ? up.GetString() : "";

                    if (string.IsNullOrEmpty(relType) || string.IsNullOrEmpty(url)) continue;

                    string displayType = relType switch {
                        "System.LinkTypes.Hierarchy-Forward" => "Child Task",
                        "System.LinkTypes.Hierarchy-Reverse" => "Parent HU",
                        "ArtifactLink" => "Branch/Commit/PR",
                        "Hyperlink" => "External Link",
                        _ => relType
                    };

                    string target = url;
                    if (relType.Contains("Hierarchy"))
                    {
                        var lastSlash = url.LastIndexOf('/');
                        if (lastSlash >= 0) target = "#" + url[(lastSlash + 1)..];
                    }

                    relTable.AddRow(displayType, Markup.Escape(target));
                    hasRel = true;
                }

                if (hasRel)
                {
                    AnsiConsole.MarkupLine("\n[bold cyan]Relations:[/]");
                    AnsiConsole.Write(relTable);
                }
            }

            AnsiConsole.WriteLine();

            var next = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title("Actions:")
                    .AddChoices("Update", "List all fields", BackOption));

            if (next == "Update")
            {
                await UpdateWorkItemInteractive(orgUrl, id);
            }
            else if (next == "List all fields")
            {
                var table = new Table().Border(TableBorder.Rounded).AddColumn("Internal Name").AddColumn("Value");
                foreach (var field in fields.EnumerateObject())
                {
                    table.AddRow(Markup.Escape(field.Name), Markup.Escape(field.Value.ToString()));
                }
                AnsiConsole.Write(table);
                AnsiConsole.Prompt(new TextPrompt<string>("Press Enter to continue...").AllowEmpty());
            }
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Error parsing details:[/] {Markup.Escape(ex.Message)}");
            return 1;
        }

        return 0;
    }

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

    private static readonly string[] ValidAzureStates = { "To Do", "Doing", "Active", "In Progress", "Resolved", "Done", "Closed", "Removed" };

    private static async Task<int> UpdateWorkItem(string orgUrl, string id, string? effort, string? remaining, string? state, string? comment, string? effortReal = null)
    {
        var fieldsList = new List<string>();
        if (!string.IsNullOrEmpty(effort)) fieldsList.Add($"{AzFieldEffortHH}={effort}");
        if (!string.IsNullOrEmpty(effortReal))
        {
            fieldsList.Add($"{AzFieldEffortRealHH}={effortReal}");
            // Also try standard field name just in case
            fieldsList.Add($"Microsoft.VSTS.Scheduling.CompletedWork={effortReal}");
        }
        if (!string.IsNullOrEmpty(remaining)) fieldsList.Add($"{AzFieldRemainingWork}={remaining}");

        if (!string.IsNullOrEmpty(state))
        {
            if (!ValidAzureStates.Contains(state, StringComparer.OrdinalIgnoreCase))
            {
                AnsiConsole.MarkupLine($"[yellow]⚠️  Warning: '{state}' may not be a valid state.[/]");
                AnsiConsole.MarkupLine($"Common states: [cyan]{string.Join(", ", ValidAzureStates)}[/]");
            }
            fieldsList.Add($"System.State={state}");
        }

        bool anySuccess = false;

        if (fieldsList.Count > 0)
        {
            var fieldsArg = string.Join(" ", fieldsList.Select(f => $"\"{f}\""));
            AnsiConsole.MarkupLine($"[dim]Updating fields for work item {id}...[/]");
            var success = await RunAzPassthrough(
                $"boards work-item update --id {id} --organization {orgUrl} --fields {fieldsArg} --output none");
            if (success)
            {
                AnsiConsole.MarkupLine($"[green]✅ Fields updated successfully.[/]");
                anySuccess = true;
            }
        }

        if (!string.IsNullOrEmpty(comment))
        {
            AnsiConsole.MarkupLine($"[dim]Adding comment to work item {id}...[/]");
            var success = await RunAzPassthrough(
                $"boards work-item update --id {id} --organization {orgUrl} --discussion \"{comment}\" --output none");
            if (success)
            {
                AnsiConsole.MarkupLine($"[green]✅ Comment added successfully.[/]");
                anySuccess = true;
            }
        }

        if (!anySuccess && fieldsList.Count == 0 && string.IsNullOrEmpty(comment))
        {
            AnsiConsole.MarkupLine("[yellow]No updates provided (use --effort, --effort-real, --remaining, --state, or --comment).[/]");
            return 1;
        }

        return anySuccess ? 0 : 1;
    }

    private static async Task<int> UpdateWorkItemInteractive(string orgUrl, string id)
    {
        while (true)
        {
            var field = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title($"[bold]Update work item #{id}[/]")
                    .AddChoices("State", "Effort (HH)", "Esfuerzo Real HH", "Remaining Work", "Add Comment", BackOption));

            if (field == BackOption) return 0;

            string? effort = null, remaining = null, state = null, comment = null, effortReal = null;

            if (field == "State")
            {
                state = AnsiConsole.Prompt(
                    new SelectionPrompt<string>()
                        .Title("Select new state:")
                        .AddChoices(ValidAzureStates)
                        .AddChoices("Other..."));

                if (state == "Other...")
                {
                    state = AnsiConsole.Prompt(new TextPrompt<string>("Enter custom state:"));
                }
            }
            else if (field == "Effort (HH)")
            {
                effort = AnsiConsole.Prompt(new TextPrompt<string>("Enter Effort (HH):"));
            }
            else if (field == "Esfuerzo Real HH")
            {
                effortReal = AnsiConsole.Prompt(new TextPrompt<string>("Enter Esfuerzo Real HH:"));
            }
            else if (field == "Remaining Work")
            {
                remaining = AnsiConsole.Prompt(new TextPrompt<string>("Enter Remaining Work:"));
            }
            else if (field == "Add Comment")
            {
                comment = AnsiConsole.Prompt(new TextPrompt<string>("Enter comment:"));
            }

            await UpdateWorkItem(orgUrl, id, effort, remaining, state, comment, effortReal);
        }
    }
}
