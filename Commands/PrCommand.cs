using Spectre.Console;
using TextCopy;

namespace YitPush;

partial class Program
{
    internal enum PrRouteKind
    {
        Menu,
        List,
        Show,
        Comments,
        Reply,
        Create,
        Ai,
        Unknown
    }

    internal sealed record PrRoute(
        PrRouteKind Kind,
        string PrId = "",
        string ThreadId = "",
        string Body = "",
        string Source = "",
        string Target = "",
        string Title = "",
        string? BodyFile = null,
        bool AutoComplete = false,
        bool Detailed = false,
        bool Save = false,
        string Language = "english",
        bool Json = false,
        bool NoSpinner = false,
        bool ValidationError = false);

    internal static class PrDispatcher
    {
        private static readonly string[] KnownSubcommands = { "list", "show", "comments", "reply", "create" };
        private static readonly string[] GlobalFlags = { "--json", "--no-spinner" };

        public static PrRoute Route(string[] args)
        {
            bool json = args.Contains("--json");
            bool noSpinner = args.Contains("--no-spinner");

            if (args.Length == 0)
                return new PrRoute(PrRouteKind.Menu, Json: json, NoSpinner: noSpinner);

            var first = args[0];
            if (KnownSubcommands.Contains(first))
                return RouteSubcommand(first, args, json, noSpinner);

            if (first.StartsWith("--", StringComparison.Ordinal) || first.StartsWith("-", StringComparison.Ordinal))
                return RouteAi(args, json, noSpinner);

            return new PrRoute(PrRouteKind.Unknown, Json: json, NoSpinner: noSpinner);
        }

        private static PrRoute RouteSubcommand(string subcommand, string[] args, bool json, bool noSpinner)
        {
            var positional = args.Skip(1).Where(a => !GlobalFlags.Contains(a)).ToArray();

            return subcommand switch
            {
                "list" => new PrRoute(PrRouteKind.List, Json: json, NoSpinner: noSpinner),
                "show" => new PrRoute(PrRouteKind.Show,
                    PrId: positional.Length > 0 ? positional[0] : "",
                    Json: json, NoSpinner: noSpinner),
                "comments" => new PrRoute(PrRouteKind.Comments,
                    PrId: positional.Length > 0 ? positional[0] : "",
                    Json: json, NoSpinner: noSpinner),
                "reply" => RouteReply(positional, args, json, noSpinner),
                "create" => RouteCreate(args, json, noSpinner),
                _ => new PrRoute(PrRouteKind.Unknown, Json: json, NoSpinner: noSpinner)
            };
        }

        private static PrRoute RouteReply(string[] positional, string[] args, bool json, bool noSpinner)
        {
            var prId = positional.Length > 0 ? positional[0] : "";
            var threadId = positional.Length > 1 ? positional[1] : "";
            var body = ExtractFlagValue(args, "--body") ?? "";

            bool validation = string.IsNullOrEmpty(prId) || string.IsNullOrEmpty(threadId) || string.IsNullOrEmpty(body);

            return new PrRoute(PrRouteKind.Reply,
                PrId: prId,
                ThreadId: threadId,
                Body: body,
                Json: json,
                NoSpinner: noSpinner,
                ValidationError: validation);
        }

        private static PrRoute RouteCreate(string[] args, bool json, bool noSpinner)
        {
            var source = ExtractFlagValue(args, "--source") ?? "";
            var target = ExtractFlagValue(args, "--target") ?? "";
            var title = ExtractFlagValue(args, "--title") ?? "";
            var bodyFile = ExtractFlagValue(args, "--body-file");
            bool autoComplete = args.Contains("--auto-complete");

            bool validation = string.IsNullOrEmpty(title) ||
                              string.IsNullOrEmpty(source) ||
                              string.IsNullOrEmpty(target);

            return new PrRoute(PrRouteKind.Create,
                Source: source,
                Target: target,
                Title: title,
                BodyFile: bodyFile,
                AutoComplete: autoComplete,
                Json: json,
                NoSpinner: noSpinner,
                ValidationError: validation);
        }

        private static PrRoute RouteAi(string[] args, bool json, bool noSpinner)
        {
            bool detailed = args.Contains("--detailed");
            bool save = args.Contains("--save");
            string language = ParseLanguage(args);
            return new PrRoute(PrRouteKind.Ai,
                Detailed: detailed,
                Save: save,
                Language: language,
                Json: json,
                NoSpinner: noSpinner);
        }

        private static string? ExtractFlagValue(string[] args, string flag)
        {
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == flag) return args[i + 1];
            }
            return null;
        }
    }

    internal static class PrAzJsonParser
    {
        public static IReadOnlyList<PrSummary> ParseList(string json)
        {
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            var root = doc.RootElement;
            var arr = root.ValueKind == System.Text.Json.JsonValueKind.Array
                ? root
                : (root.TryGetProperty("value", out var v) ? v : default);

            if (arr.ValueKind != System.Text.Json.JsonValueKind.Array)
                return Array.Empty<PrSummary>();

            var list = new List<PrSummary>();
            foreach (var item in arr.EnumerateArray())
            {
                var pr = new PrSummary
                {
                    Id = item.TryGetProperty("pullRequestId", out var id) ? id.ToString() : "",
                    Title = item.TryGetProperty("title", out var t) ? t.GetString() ?? "" : "",
                    Author = ExtractAuthor(item, "createdBy"),
                    SourceBranch = StripRefsHeads(item, "sourceRefName"),
                    TargetBranch = StripRefsHeads(item, "targetRefName"),
                    IsDraft = item.TryGetProperty("isDraft", out var d) && d.ValueKind == System.Text.Json.JsonValueKind.True,
                    CreationDate = item.TryGetProperty("creationDate", out var cd) ? cd.GetString() ?? "" : ""
                };
                list.Add(pr);
            }
            return list;
        }

        public static PrDetail? ParseShow(string json)
        {
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            var root = doc.RootElement;

            var detail = new PrDetail
            {
                Id = root.TryGetProperty("pullRequestId", out var id) ? id.ToString() : "",
                Title = root.TryGetProperty("title", out var t) ? t.GetString() ?? "" : "",
                Description = root.TryGetProperty("description", out var d) ? d.GetString() ?? "" : "",
                SourceBranch = StripRefsHeads(root, "sourceRefName"),
                TargetBranch = StripRefsHeads(root, "targetRefName"),
                Author = ExtractAuthor(root, "createdBy"),
                Status = root.TryGetProperty("status", out var s) ? s.GetString() ?? "" : "",
                IsDraft = root.TryGetProperty("isDraft", out var dr) && dr.ValueKind == System.Text.Json.JsonValueKind.True,
                CreationDate = root.TryGetProperty("creationDate", out var cd) ? cd.GetString() ?? "" : ""
            };

            if (root.TryGetProperty("reviewers", out var revs) && revs.ValueKind == System.Text.Json.JsonValueKind.Array)
            {
                foreach (var r in revs.EnumerateArray())
                {
                    var reviewer = new PrReviewer
                    {
                        DisplayName = r.TryGetProperty("displayName", out var dn) ? dn.GetString() ?? "" : "",
                        Vote = r.TryGetProperty("vote", out var v) && v.ValueKind == System.Text.Json.JsonValueKind.Number ? v.GetInt32() : 0,
                        IsRequired = r.TryGetProperty("isRequired", out var ir) && ir.ValueKind == System.Text.Json.JsonValueKind.True
                    };
                    detail.Reviewers.Add(reviewer);
                }
            }

            return detail;
        }

        private static string ExtractAuthor(System.Text.Json.JsonElement parent, string propName)
        {
            if (!parent.TryGetProperty(propName, out var obj) || obj.ValueKind != System.Text.Json.JsonValueKind.Object)
                return "";
            if (obj.TryGetProperty("displayName", out var dn)) return dn.GetString() ?? "";
            if (obj.TryGetProperty("uniqueName", out var un)) return un.GetString() ?? "";
            return "";
        }

        private static string StripRefsHeads(System.Text.Json.JsonElement parent, string propName)
        {
            if (!parent.TryGetProperty(propName, out var v) || v.ValueKind != System.Text.Json.JsonValueKind.String)
                return "";
            var raw = v.GetString() ?? "";
            const string prefix = "refs/heads/";
            return raw.StartsWith(prefix, StringComparison.Ordinal) ? raw[prefix.Length..] : raw;
        }
    }

    private static async Task<int> PrCommand(string[] args)
    {
        var route = PrDispatcher.Route(args);

        return route.Kind switch
        {
            PrRouteKind.Menu => await PrMenuAsync(route),
            PrRouteKind.List => await PrListCliAsync(route),
            PrRouteKind.Show => await PrShowCliAsync(route),
            PrRouteKind.Comments => await PrCommentsCliAsync(route),
            PrRouteKind.Reply => await PrReplyCliAsync(route),
            PrRouteKind.Create => await PrCreateCliAsync(route),
            PrRouteKind.Ai => await PrAiCliAsync(route),
            _ => PrUnknownUsage(args, route)
        };
    }

    private static int PrUnknownUsage(string[] args, PrRoute route)
    {
        if (route.ValidationError)
        {
            AnsiConsole.MarkupLine("[red]❌ Invalid arguments for the PR subcommand.[/]");
            if (args.Length > 0)
                AnsiConsole.MarkupLine($"[dim]Command:[/] yp pr {Markup.Escape(string.Join(" ", args))}");
        }
        else
        {
            AnsiConsole.MarkupLine($"[red]❌ Unknown pr subcommand:[/] {Markup.Escape(args[0])}");
        }
        AnsiConsole.MarkupLine("[dim]Run `yp pr` to see the interactive menu, or `yp --help` for the full command list.[/]");
        return 3;
    }

    // ─── Interactive menu (default `yp pr` with no args) ──────────────────────

    private static async Task<int> PrMenuAsync(PrRoute route)
    {
        if (Console.IsInputRedirected || Console.IsOutputRedirected)
        {
            AnsiConsole.MarkupLine("[bold]yp pr[/] [dim]— pick a subcommand:[/]\n");
            AnsiConsole.MarkupLine($"  [cyan]list[/]                                {Markup.Escape("List open pull requests in the current repo")}");
            AnsiConsole.MarkupLine($"  [cyan]show <pr-id>[/]                        {Markup.Escape("Show a single PR (title, description, reviewers)")}");
            AnsiConsole.MarkupLine($"  [cyan]comments <pr-id>[/]                    {Markup.Escape("List discussion threads (REST API)")}");
            AnsiConsole.MarkupLine($"  [cyan]reply <pr-id> <thread-id> --body \"...\"[/]   {Markup.Escape("Reply to a thread (REST API)")}");
            AnsiConsole.MarkupLine($"  [cyan]create[/] {Markup.Escape("--source <b> --target <b> --title <t> [--body-file <p>] [--auto-complete]")}");
            AnsiConsole.MarkupLine($"                                          {Markup.Escape("Open a new PR (REST API)")}");
            AnsiConsole.MarkupLine($"  [cyan]--detailed -l <lang> --save[/]        {Markup.Escape("Generate an AI description (no subcommand)")}");
            AnsiConsole.MarkupLine("\n[dim]Tip: in an interactive terminal, running `yp pr` opens a menu. In a pipe / CI, it prints this list instead.[/]");
            return 0;
        }

        const string AiOption = "🤖  Generate AI-powered PR description";
        const string ListOption = "📋  List open PRs";
        const string ShowOption = "🔍  Show PR details";
        const string CommentsOption = "💬  Show PR comments / threads";
        const string ReplyOption = "↩️   Reply to a thread";
        const string CreateOption = "➕  Create a new PR";

        var choices = new List<string>
        {
            AiOption, ListOption, ShowOption, CommentsOption, ReplyOption, CreateOption, BackOption
        };

        while (true)
        {
            var choice = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title("[bold cyan]yp pr[/] [dim]— pick an action:[/]")
                    .PageSize(10)
                    .HighlightStyle(new Style(Color.Cyan1))
                    .AddChoices(choices));

            if (choice == BackOption) return 0;

            if (choice == AiOption)
                return await PrAiCliAsync(new PrRoute(PrRouteKind.Ai, Detailed: false, Save: false, Language: "english"));
            if (choice == ListOption)
                return await PrListCliAsync(new PrRoute(PrRouteKind.List, Json: route.Json));
            if (choice == ShowOption)
                return await PrShowPromptCliAsync(route);
            if (choice == CommentsOption)
                return await PrCommentsPromptCliAsync(route);
            if (choice == ReplyOption)
                return await PrReplyPromptCliAsync(route);
            if (choice == CreateOption)
                return await PrCreatePromptCliAsync(route);
        }
    }

    // ─── Read: list ───────────────────────────────────────────────────────────

    internal static async Task<int> PrListCliAsync(PrRoute route)
    {
        var (output, error) = await RunAzCaptureWithError("repos pr list --output json --status all");
        if (output == null)
        {
            if (route.Json)
                Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(new { exitCode = 2, error = error ?? "az failed" }));
            else
                AnsiConsole.MarkupLine($"[red]❌ Failed to list PRs:[/] {Markup.Escape(error ?? "az exited without output")}");
            return 2;
        }

        var prs = PrAzJsonParser.ParseList(output);

        if (route.Json)
        {
            var payload = new
            {
                exitCode = 0,
                count = prs.Count,
                pullRequests = prs
            };
            Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(payload,
                new System.Text.Json.JsonSerializerOptions { WriteIndented = false }));
            return 0;
        }

        if (prs.Count == 0)
        {
            AnsiConsole.MarkupLine("[dim]No pull requests found.[/]");
            return 0;
        }

        var table = new Table()
            .Border(TableBorder.Rounded)
            .BorderColor(Color.Cyan1)
            .AddColumn(new TableColumn("[bold]ID[/]").NoWrap())
            .AddColumn(new TableColumn("[bold]Title[/]"))
            .AddColumn(new TableColumn("[bold]Author[/]").NoWrap())
            .AddColumn(new TableColumn("[bold]Source[/]").NoWrap())
            .AddColumn(new TableColumn("[bold]Target[/]").NoWrap())
            .AddColumn(new TableColumn("[bold]Draft[/]").NoWrap())
            .AddColumn(new TableColumn("[bold]Created[/]").NoWrap());

        foreach (var p in prs)
        {
            table.AddRow(
                Markup.Escape(p.Id),
                Markup.Escape(p.Title),
                Markup.Escape(p.Author),
                Markup.Escape(p.SourceBranch),
                Markup.Escape(p.TargetBranch),
                p.IsDraft ? "[yellow]draft[/]" : "—",
                Markup.Escape(p.CreationDate));
        }

        AnsiConsole.Write(table);
        return 0;
    }

    // ─── Read: show ──────────────────────────────────────────────────────────

    private static async Task<int> PrShowPromptCliAsync(PrRoute route)
    {
        var prId = AnsiConsole.Prompt(new TextPrompt<string>("PR id:"));
        return await PrShowCliAsync(new PrRoute(route.Kind, PrId: prId, Json: route.Json));
    }

    internal static async Task<int> PrShowCliAsync(PrRoute route)
    {
        if (string.IsNullOrEmpty(route.PrId))
        {
            AnsiConsole.MarkupLine("[red]❌ Usage: yp pr show <pr-id>[/]");
            return 3;
        }

        var (output, error) = await RunAzCaptureWithError($"repos pr show --id {route.PrId} --output json");
        if (output == null)
        {
            var (exitCode, msg) = ClassifyAzError(error, "show", route.PrId);
            if (route.Json)
                Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(new { exitCode, error = msg, prId = route.PrId }));
            else
                AnsiConsole.MarkupLine($"[red]❌ {Markup.Escape(msg)}[/]");
            return exitCode;
        }

        var detail = PrAzJsonParser.ParseShow(output);
        if (detail == null || string.IsNullOrEmpty(detail.Id))
        {
            if (route.Json)
                Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(new { exitCode = 1, error = "PR not found", prId = route.PrId }));
            else
                AnsiConsole.MarkupLine($"[red]❌ PR {Markup.Escape(route.PrId)} not found.[/]");
            return 1;
        }

        if (route.Json)
        {
            var payload = new
            {
                exitCode = 0,
                pullRequest = detail
            };
            Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(payload,
                new System.Text.Json.JsonSerializerOptions { WriteIndented = false }));
            return 0;
        }

        var panel = new Panel(new Markup(
            $"[bold]{Markup.Escape(detail.Title)}[/]\n" +
            $"[dim]#{Markup.Escape(detail.Id)}  •  {Markup.Escape(detail.SourceBranch)} → {Markup.Escape(detail.TargetBranch)}[/]\n" +
            $"[dim]by {Markup.Escape(detail.Author)} on {Markup.Escape(detail.CreationDate)}  •  status: {Markup.Escape(detail.Status)}[/]" +
            (detail.IsDraft ? "  [yellow](draft)[/]" : "")))
            .Header($"[cyan]PR #{Markup.Escape(detail.Id)}[/]")
            .BorderColor(Color.Cyan1)
            .Padding(1, 0);
        AnsiConsole.Write(panel);

        if (!string.IsNullOrWhiteSpace(detail.Description))
        {
            AnsiConsole.MarkupLine("\n[bold]Description[/]");
            AnsiConsole.WriteLine(detail.Description);
        }

        if (detail.Reviewers.Count > 0)
        {
            AnsiConsole.MarkupLine("\n[bold]Reviewers[/]");
            var rt = new Table().Border(TableBorder.Rounded).BorderColor(Color.Grey)
                .AddColumn("[bold]Name[/]").AddColumn("[bold]Vote[/]").AddColumn("[bold]Required[/]");
            foreach (var r in detail.Reviewers)
                rt.AddRow(Markup.Escape(r.DisplayName), r.Vote.ToString(), r.IsRequired ? "[red]yes[/]" : "no");
            AnsiConsole.Write(rt);
        }

        return 0;
    }

    // ─── Read: comments / threads (REST API) ────────────────────────────────

    private static async Task<int> PrCommentsPromptCliAsync(PrRoute route)
    {
        var prId = AnsiConsole.Prompt(new TextPrompt<string>("PR id:"));
        return await PrCommentsCliAsync(new PrRoute(route.Kind, PrId: prId, Json: route.Json));
    }

    internal static async Task<int> PrCommentsCliAsync(PrRoute route)
    {
        if (string.IsNullOrEmpty(route.PrId))
        {
            AnsiConsole.MarkupLine("[red]❌ Usage: yp pr comments <pr-id>[/]");
            return 3;
        }

        var ctx = await ResolveAzureDevOpsPrContextAsync();
        if (ctx == null)
        {
            if (route.Json)
                Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(new { exitCode = 2, error = "Could not resolve Azure DevOps context (org/project/repo). Run `az devops configure --defaults organization=... project=...` and `az login`." }));
            else
                AnsiConsole.MarkupLine("[red]❌ Could not resolve Azure DevOps context. Run `az login` and ensure the default org/project are configured.[/]");
            return 2;
        }

        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(ApiTimeoutSeconds) };
        var (token, authError) = await GetAzureAccessTokenWithError();
        if (token == null)
        {
            if (route.Json)
                Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(new { exitCode = 2, error = authError ?? "auth failed" }));
            else
                AnsiConsole.MarkupLine($"[red]❌ {Markup.Escape(authError ?? "Failed to get Azure access token. Run `az login` first.")}[/]");
            return 2;
        }
        http.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");

        var result = await AzDevOpsPrClient.ListThreadsAsync(http, ctx.OrgUrl, ctx.ProjectId, ctx.RepoId, int.Parse(route.PrId));

        if (route.Json)
        {
            var payload = new
            {
                exitCode = result.ExitCode,
                prId = route.PrId,
                threads = result.Threads,
                error = result.Error
            };
            Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(payload,
                new System.Text.Json.JsonSerializerOptions { WriteIndented = false }));
            return result.ExitCode;
        }

        if (result.ExitCode != 0)
        {
            AnsiConsole.MarkupLine($"[red]❌ {Markup.Escape(result.Error ?? "Failed to list threads")}[/]");
            return result.ExitCode;
        }

        if (result.Threads.Count == 0)
        {
            AnsiConsole.MarkupLine("[dim]No threads on this PR.[/]");
            return 0;
        }

        foreach (var thread in result.Threads)
        {
            var location = !string.IsNullOrEmpty(thread.FilePath)
                ? $" [dim]{Markup.Escape(thread.FilePath)}:{thread.LineNumber}[/]"
                : "";
            AnsiConsole.Write(new Panel(new Markup(
                $"[bold]Thread {thread.Id}[/] [dim]({Markup.Escape(thread.Status)})[/]{location}\n\n" +
                string.Join("\n\n", thread.Comments.Select(c =>
                    $"[cyan]{Markup.Escape(c.Author)}[/] [dim]({Markup.Escape(c.Date)})[/]\n{Markup.Escape(c.Content)}"))))
                .Header($"[cyan]Thread #{thread.Id}[/]")
                .BorderColor(Color.Grey)
                .Padding(1, 1));
        }
        return 0;
    }

    // ─── Write: reply (REST API) ─────────────────────────────────────────────

    private static async Task<int> PrReplyPromptCliAsync(PrRoute route)
    {
        var prId = AnsiConsole.Prompt(new TextPrompt<string>("PR id:"));
        var threadId = AnsiConsole.Prompt(new TextPrompt<string>("Thread id:"));
        var body = AnsiConsole.Prompt(new TextPrompt<string>("Reply body:"));
        return await PrReplyCliAsync(new PrRoute(route.Kind, PrId: prId, ThreadId: threadId, Body: body, Json: route.Json));
    }

    internal static async Task<int> PrReplyCliAsync(PrRoute route)
    {
        if (route.ValidationError || string.IsNullOrEmpty(route.PrId) || string.IsNullOrEmpty(route.ThreadId) || string.IsNullOrEmpty(route.Body))
        {
            AnsiConsole.MarkupLine("[red]❌ Usage: yp pr reply <pr-id> <thread-id> --body \"...\"[/]");
            return 3;
        }

        var ctx = await ResolveAzureDevOpsPrContextAsync();
        if (ctx == null)
        {
            if (route.Json)
                Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(new { exitCode = 2, error = "Could not resolve Azure DevOps context" }));
            else
                AnsiConsole.MarkupLine("[red]❌ Could not resolve Azure DevOps context.[/]");
            return 2;
        }

        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(ApiTimeoutSeconds) };
        var (token, authError) = await GetAzureAccessTokenWithError();
        if (token == null)
        {
            if (route.Json)
                Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(new { exitCode = 2, error = authError ?? "auth failed" }));
            else
                AnsiConsole.MarkupLine($"[red]❌ {Markup.Escape(authError ?? "Failed to get Azure access token. Run `az login` first.")}[/]");
            return 2;
        }
        http.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");

        var result = await AzDevOpsPrClient.PostThreadCommentAsync(http, ctx.OrgUrl, ctx.ProjectId, ctx.RepoId,
            int.Parse(route.PrId), int.Parse(route.ThreadId), route.Body);

        if (route.Json)
        {
            Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(new
            {
                exitCode = result.ExitCode,
                prId = route.PrId,
                threadId = route.ThreadId,
                error = result.Error
            }, new System.Text.Json.JsonSerializerOptions { WriteIndented = false }));
            return result.ExitCode;
        }

        if (result.ExitCode == 0)
        {
            AnsiConsole.MarkupLine($"[green]✅ Reply posted to thread {Markup.Escape(route.ThreadId)} of PR {Markup.Escape(route.PrId)}.[/]");
            return 0;
        }

        AnsiConsole.MarkupLine($"[red]❌ {Markup.Escape(result.Error ?? "Failed to post reply")}[/]");
        return result.ExitCode;
    }

    // ─── Write: create (REST API) ────────────────────────────────────────────

    private static async Task<int> PrCreatePromptCliAsync(PrRoute route)
    {
        var source = AnsiConsole.Prompt(new TextPrompt<string>("Source branch:"));
        var target = AnsiConsole.Prompt(new TextPrompt<string>("Target branch:"));
        var title = AnsiConsole.Prompt(new TextPrompt<string>("PR title:"));
        var useFile = AnsiConsole.Prompt(new ConfirmationPrompt("Load description from a file?"));
        string? bodyFile = null;
        string? stdinBody = null;
        if (useFile)
            bodyFile = AnsiConsole.Prompt(new TextPrompt<string>("Body file path:"));
        else
            stdinBody = AnsiConsole.Prompt(new TextPrompt<string>("Description (paste, then Enter):"));

        return await PrCreateCliAsync(new PrRoute(route.Kind,
            Source: source, Target: target, Title: title,
            BodyFile: bodyFile, Body: stdinBody ?? "",
            Json: route.Json));
    }

    internal static async Task<int> PrCreateCliAsync(PrRoute route)
    {
        if (route.ValidationError)
        {
            AnsiConsole.MarkupLine($"[red]❌ {Markup.Escape("Usage: yp pr create --source <branch> --target <branch> --title <title> [--body-file <path>] [--auto-complete]")}[/]");
            return 3;
        }

        string description;
        if (!string.IsNullOrEmpty(route.BodyFile))
        {
            if (!File.Exists(route.BodyFile))
            {
                if (route.Json)
                    Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(new { exitCode = 2, error = $"File not found: {route.BodyFile}" }));
                else
                    AnsiConsole.MarkupLine($"[red]❌ File not found:[/] {Markup.Escape(route.BodyFile)}");
                return 2;
            }
            description = await File.ReadAllTextAsync(route.BodyFile);
        }
        else
        {
            description = route.Body;
        }

        var ctx = await ResolveAzureDevOpsPrContextAsync();
        if (ctx == null)
        {
            if (route.Json)
                Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(new { exitCode = 2, error = "Could not resolve Azure DevOps context" }));
            else
                AnsiConsole.MarkupLine("[red]❌ Could not resolve Azure DevOps context.[/]");
            return 2;
        }

        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(ApiTimeoutSeconds) };
        var (token, authError) = await GetAzureAccessTokenWithError();
        if (token == null)
        {
            if (route.Json)
                Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(new { exitCode = 2, error = authError ?? "auth failed" }));
            else
                AnsiConsole.MarkupLine($"[red]❌ {Markup.Escape(authError ?? "Failed to get Azure access token. Run `az login` first.")}[/]");
            return 2;
        }
        http.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");

        var result = await AzDevOpsPrClient.CreatePullRequestAsync(http, ctx.OrgUrl, ctx.ProjectId, ctx.RepoId,
            new AzDevOpsPrClient.CreatePrRequest(
                SourceRefName: $"refs/heads/{route.Source}",
                TargetRefName: $"refs/heads/{route.Target}",
                Title: route.Title,
                Description: description,
                AutoComplete: route.AutoComplete));

        if (route.Json)
        {
            var payload = new
            {
                exitCode = result.ExitCode,
                pullRequestId = result.PullRequestId,
                error = result.Error
            };
            Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(payload,
                new System.Text.Json.JsonSerializerOptions { WriteIndented = false }));
            return result.ExitCode;
        }

        if (result.ExitCode == 0)
        {
            AnsiConsole.MarkupLine($"[green]✅ Pull request #[bold]{Markup.Escape(result.PullRequestId ?? "?")}[/] created.[/]");
            AnsiConsole.MarkupLine($"[dim]Open it in the browser:[/] [cyan]{Markup.Escape(ctx.OrgUrl)}/{Markup.Escape(ctx.ProjectId)}/_git/{Markup.Escape(ctx.RepoName ?? ctx.RepoId)}/pullrequest/{Markup.Escape(result.PullRequestId ?? "")}[/]");
            return 0;
        }

        AnsiConsole.MarkupLine($"[red]❌ {Markup.Escape(result.Error ?? "Failed to create PR")}[/]");
        return result.ExitCode;
    }

    // ─── AI description (backward compat: `yp pr --detailed` etc.) ─────────

    private static async Task<int> PrAiCliAsync(PrRoute route)
    {
        if (!await IsGitRepository())
        {
            AnsiConsole.MarkupLine("[red]❌ Error: Not a git repository.[/]");
            Console.WriteLine("\nPlease run this command from within a git repository.");
            return 1;
        }

        if (Console.IsInputRedirected || Console.IsOutputRedirected)
        {
            AnsiConsole.MarkupLine("[red]❌ The AI description flow needs an interactive terminal[/] [dim](stdin/stdout is redirected).[/]");
            AnsiConsole.MarkupLine("[dim]Run from a real terminal, or use `yp pr create --body-file ...` to open a PR from a script.[/]");
            return 1;
        }

        return await GeneratePrDescription(route.Detailed, route.Language, route.Save, route.NoSpinner);
    }

    // ─── Azure DevOps context (org / project / repo) for REST API calls ─────

    internal sealed record PrContext(string OrgUrl, string ProjectId, string RepoId, string RepoName);

    private static async Task<PrContext?> ResolveAzureDevOpsPrContextAsync()
    {
        var orgJson = await RunAzCapture("devops configure --list");
        if (orgJson == null) return null;

        string? orgName = ExtractConfigValue(orgJson, "organization");
        string? defaultProject = ExtractConfigValue(orgJson, "project");
        if (string.IsNullOrEmpty(orgName) || string.IsNullOrEmpty(defaultProject))
            return null;

        var orgUrl = $"https://dev.azure.com/{orgName}";

        var projectJson = await RunAzCapture($"devops project show --project \"{defaultProject}\" --organization {orgUrl} --output json");
        if (projectJson == null) return null;
        string? projectId = ExtractProjectId(projectJson);
        if (string.IsNullOrEmpty(projectId)) return null;

        var remoteUrl = await TryGetOriginRemoteUrl();
        string? repoName = ExtractRepoNameFromRemote(remoteUrl);
        if (string.IsNullOrEmpty(repoName))
        {
            AnsiConsole.MarkupLine("[yellow]⚠️  Could not detect the repo name from `git remote`. Falling back to the default repo from `az devops configure`.[/]");
            var defaultRepo = ExtractConfigValue(orgJson, "repository");
            repoName = defaultRepo;
        }
        if (string.IsNullOrEmpty(repoName)) return null;

        var repoJson = await RunAzCapture($"repos show --repository \"{repoName}\" --organization {orgUrl} --project \"{projectId}\" --output json");
        if (repoJson == null) return null;
        string? repoId = ExtractRepoId(repoJson);
        if (string.IsNullOrEmpty(repoId)) return null;

        return new PrContext(orgUrl, projectId, repoId, repoName);
    }

    private static async Task<string?> TryGetOriginRemoteUrl()
    {
        try
        {
            var psi = new System.Diagnostics.ProcessStartInfo("git", "remote get-url origin")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var p = System.Diagnostics.Process.Start(psi)!;
            var url = await p.StandardOutput.ReadToEndAsync();
            await p.WaitForExitAsync();
            return p.ExitCode == 0 ? url.Trim() : null;
        }
        catch
        {
            return null;
        }
    }

    private static string? ExtractRepoNameFromRemote(string? remoteUrl)
    {
        if (string.IsNullOrEmpty(remoteUrl)) return null;
        // Supports https://dev.azure.com/<org>/<project>/_git/<repo>
        // and git@ssh.dev.azure.com:v3/<org>/<project>/<repo>
        var marker = "_git/";
        var idx = remoteUrl.IndexOf(marker, StringComparison.Ordinal);
        if (idx >= 0)
        {
            var name = remoteUrl[(idx + marker.Length)..].TrimEnd('/');
            if (name.EndsWith(".git", StringComparison.OrdinalIgnoreCase))
                name = name[..^4];
            return name;
        }
        var parts = remoteUrl.TrimEnd('/').Split('/');
        return parts.Length > 0 ? parts[^1] : null;
    }

    private static string? ExtractConfigValue(string azConfigJson, string key)
    {
        // `az devops configure --list` emits INI-like lines: "key = value".
        foreach (var raw in azConfigJson.Split('\n'))
        {
            var line = raw.Trim();
            if (line.StartsWith(key + " ", StringComparison.OrdinalIgnoreCase) ||
                line.StartsWith(key + "=", StringComparison.OrdinalIgnoreCase))
            {
                var sep = line.IndexOf('=');
                if (sep > 0)
                    return line[(sep + 1)..].Trim();
            }
        }
        return null;
    }

    private static string? ExtractProjectId(string projectJson)
    {
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(projectJson);
            if (doc.RootElement.TryGetProperty("id", out var id)) return id.GetString();
        }
        catch { }
        return null;
    }

    private static string? ExtractRepoId(string repoJson)
    {
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(repoJson);
            if (doc.RootElement.TryGetProperty("id", out var id)) return id.GetString();
        }
        catch { }
        return null;
    }

    private static (int ExitCode, string Message) ClassifyAzError(string? error, string action, string id)
    {
        if (string.IsNullOrEmpty(error)) return (1, $"Failed to {action} PR {id}.");
        if (error.Contains("not found", StringComparison.OrdinalIgnoreCase) ||
            error.Contains("does not exist", StringComparison.OrdinalIgnoreCase) ||
            error.Contains("404", StringComparison.Ordinal))
        {
            return (1, $"PR {id} not found.");
        }
        if (error.Contains("unauthorized", StringComparison.OrdinalIgnoreCase) ||
            error.Contains("401", StringComparison.Ordinal))
        {
            return (2, $"Authentication failed. Run `az login` first.");
        }
        return (2, error);
    }

    private static async Task<(string? Token, string? Error)> GetAzureAccessTokenWithError()
    {
        // Re-uses the existing GetAzureAccessToken — but that helper doesn't surface
        // errors. We shell out to `az account get-access-token` ourselves so the
        // error message can flow through to the JSON envelope.
        var (output, error) = await RunAzCaptureWithError("account get-access-token --resource 499b84ac-1321-427f-aa17-267ca6975798 --output json");
        if (output == null) return (null, error ?? "Failed to get Azure access token");
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(output);
            if (doc.RootElement.TryGetProperty("accessToken", out var token))
                return (token.GetString(), null);
        }
        catch { }
        return (null, "Could not parse access token response");
    }


    private static async Task<string> GeneratePrDescriptionContent(string diff, bool detailed, string language)
    {
        const int reservedTokens = ApiMaxTokens + ApiMaxTokens; // extra buffer for system prompt + formatting
        const int maxPromptTokens = ApiMaxContextTokens - reservedTokens;
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

        return await CallAiApi(prompt);
    }

    private static async Task<int> GeneratePrDescription(bool detailed, string language, bool save, bool noSpinner = false)
    {

        // Get AI provider info
        var (_, aiModel, aiProviderName, _) = GetAiInfo();
        if (string.IsNullOrEmpty(aiProviderName))
        {
            AnsiConsole.MarkupLine("[red]❌ No AI provider configured.[/]");
            AnsiConsole.MarkupLine("Run [cyan]yp setup[/] to configure your AI provider.");
            AnsiConsole.MarkupLine("[dim]Or set DEEPSEEK_API_KEY environment variable for DeepSeek (backward compatible).[/]");
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
                    .EnableSearch()
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
                    .EnableSearch()
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
        AnsiConsole.MarkupLine($"🤖 Generating PR description with {aiProviderName} ({aiModel})...{(detailed ? " (detailed mode)" : "")}");
        var description = await Ui.RunWithStatus(
            "Generating PR description…",
            _ => GeneratePrDescriptionContent(diff, detailed, language),
            noSpinner: noSpinner);

        if (string.IsNullOrWhiteSpace(description))
        {
            AnsiConsole.MarkupLine("[red]❌ Error: Failed to generate PR description.[/]");
            return 1;
        }

        AnsiConsole.WriteLine();
        AnsiConsole.Write(new Panel(new Text(description))
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
}
