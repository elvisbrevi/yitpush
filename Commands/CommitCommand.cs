using System.Text;
using System.Text.RegularExpressions;
using Spectre.Console;
using TextCopy;

namespace YitPush;

partial class Program
{
    private static async Task<int> CommitCommand(string[] args)
    {
        CommitArgs commitArgs;
        try
        {
            // Pull the latest config so AppConfig.CommitFormat is honored end-to-end.
            var config = TryLoadConfigForCommitFormat();
            commitArgs = ParseCommitArgs(args, config);
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]❌ Invalid --template:[/] {Markup.Escape(ex.Message)}");
            return 6;
        }

        try
        {
            if (!await IsGitRepository())
            {
                AnsiConsole.MarkupLine("[red]❌ Error: Not a git repository.[/]");
                Console.WriteLine("\nPlease run this command from within a git repository.");
                return 1;
            }

            var (aiApiKey, aiModel, aiProviderName, _) = GetAiInfo();
            if (string.IsNullOrEmpty(aiApiKey))
            {
                AnsiConsole.MarkupLine("[red]❌ No AI provider configured.[/]");
                AnsiConsole.MarkupLine("Run [cyan]yp setup[/] to configure your AI provider.");
                AnsiConsole.MarkupLine("[dim]Or set DEEPSEEK_API_KEY environment variable for DeepSeek (backward compatible).[/]");
                return 1;
            }

            // --amend re-runs the AI against the LAST commit, not the working tree.
            string diff;
            bool hasChanges;
            if (commitArgs.Amend)
            {
                Console.WriteLine("📊 Analyzing previous commit (--amend)...");
                diff = await RunGitOutput("diff HEAD~1");
                hasChanges = !string.IsNullOrWhiteSpace(diff);
                if (!hasChanges)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("❌ Error: --amend requires at least one prior commit (nothing in HEAD~1..HEAD).");
                    Console.ResetColor();
                    return 1;
                }
                Console.WriteLine($"Found previous commit diff ({diff.Length} characters)\n");
            }
            else
            {
                Console.WriteLine("📊 Analyzing git changes...");
                diff = await GetGitDiff();
                hasChanges = !string.IsNullOrWhiteSpace(diff);

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
            }

            string? commitMessage = null;

            if (hasChanges)
            {
                var breakingMarkers = commitArgs.DetectBreaking
                    ? DetectBreakingChanges(diff)
                    : Array.Empty<string>();

                Console.WriteLine($"🤖 Generating commit message with {aiProviderName} ({aiModel})...{(commitArgs.Detailed ? " (detailed mode)" : "")}{(commitArgs.Conventional ? " (conventional commits)" : "")}{(commitArgs.DetectBreaking ? " (breaking-change detection)" : "")}");
                var rawAi = await Ui.RunWithStatus(
                    "Generating commit message…",
                    _ => GenerateCommitMessage(diff, commitArgs),
                    noSpinner: commitArgs.NoSpinner);
                if (string.IsNullOrWhiteSpace(rawAi))
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("❌ Error: Failed to generate commit message.");
                    Console.ResetColor();
                    return 1;
                }

                try
                {
                    commitMessage = BuildCommitMessage(rawAi, commitArgs, breakingMarkers);
                }
                catch (FileNotFoundException ex)
                {
                    // Issue #11 AC: --template path that doesn't exist -> exit 6.
                    AnsiConsole.MarkupLine("[red]❌ --template file not found:[/] " + Markup.Escape(ex.FileName ?? ex.Message));
                    return 6;
                }

                if (string.IsNullOrWhiteSpace(commitMessage))
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("❌ Error: Built commit message is empty.");
                    Console.ResetColor();
                    return 1;
                }

                Console.WriteLine("\n📝 Generated commit message:");
                AnsiConsole.Write(new Panel(new Text(commitMessage))
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

                if (commitArgs.Save)
                {
                    var fileName = $"commit-message-{DateTime.Now:yyyyMMdd-HHmmss}.md";
                    await File.WriteAllTextAsync(fileName, commitMessage);
                    AnsiConsole.MarkupLine($"[green]✅ Commit message saved to:[/] {fileName}\n");
                }

                if (commitArgs.RequireConfirmation)
                {
                    Console.Write(commitArgs.Amend
                        ? "Do you want to proceed with the amend? (y/n): "
                        : "Do you want to proceed with this commit? (y/n): ");
                    var response = Console.ReadLine()?.Trim().ToLower();
                    if (response != "y" && response != "yes")
                    {
                        Console.WriteLine("\n❌ Cancelled.");
                        return 0;
                    }
                }
                else
                {
                    Console.WriteLine(commitArgs.Amend
                        ? "⏩ Proceeding automatically with amend (use --confirm to review)..."
                        : "⏩ Proceeding automatically (use --confirm to review)...");
                }
            }
            else
            {
                if (commitArgs.RequireConfirmation)
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

            if (commitArgs.Amend)
            {
                // --amend: replace the last commit's message in place. No `git add`, no push.
                Console.WriteLine("   git commit --amend -F <temp>");
                if (!await ExecuteGitCommitAmendWithMessage(commitMessage!))
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("❌ Error: git commit --amend failed.");
                    Console.ResetColor();
                    return 1;
                }

                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("\n✅ Successfully amended last commit (no push performed).");
                Console.ResetColor();
                return 0;
            }

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
                if (!await ExecuteGitCommitWithMessage(commitMessage!))
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("❌ Error: git commit failed.");
                    Console.ResetColor();
                    return 1;
                }
            }

            if (!await Ui.RunWithStatus(
                    "Pushing to origin…",
                    _ => ExecuteGitPush(commitArgs.RequireConfirmation),
                    noSpinner: commitArgs.NoSpinner))
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

    // Loads the user config but never throws — commit workflow must keep working
    // even when there's no ~/.yitpush/config.json (e.g. env-only setups).
    private static AppConfig TryLoadConfigForCommitFormat()
    {
        try
        {
            return ConfigManager.Load();
        }
        catch
        {
            return new AppConfig();
        }
    }

    private static async Task<string> GenerateCommitMessage(string diff, CommitArgs args)
    {
        const int reservedTokens = ApiMaxTokens + ApiMaxTokens; // extra buffer for system prompt + formatting
        const int maxPromptTokens = ApiMaxContextTokens - reservedTokens;
        const int averageCharsPerToken = 3; // conservative estimate for code diffs
        const int maxPromptChars = maxPromptTokens * averageCharsPerToken;

        diff = TruncateDiff(diff, maxPromptChars);

        var markers = args.DetectBreaking ? DetectBreakingChanges(diff) : Array.Empty<string>();
        var prompt = BuildCommitPrompt(args, diff, markers);

        return await CallAiApi(prompt);
    }

    private static async Task<bool> ExecuteGitCommitAmendWithMessage(string commitMessage)
    {
        string tempFile = Path.Combine(Path.GetTempPath(), $"git_amend_msg_{Guid.NewGuid():N}.txt");
        try
        {
            await File.WriteAllTextAsync(tempFile, commitMessage, Encoding.UTF8);
            return await ExecuteGitCommand($"commit --amend -F \"{tempFile}\"");
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
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

    // Handlebars-ish {{var}} substitution. Unknown tokens are left untouched so the
    // user can keep literal {{...}} in the template. Missing values become empty strings.
    private static string RenderCommitTemplate(string template, IDictionary<string, string>? values)
    {
        if (string.IsNullOrEmpty(template)) return string.Empty;
        if (values == null) return template;

        return Regex.Replace(template, @"\{\{\s*([a-zA-Z_][a-zA-Z0-9_]*)\s*\}\}", match =>
        {
            var key = match.Groups[1].Value;
            return values.TryGetValue(key, out var v) ? v : string.Empty;
        });
    }

    private static readonly string[] ConventionalAllowedTypes =
    {
        "feat", "fix", "chore", "refactor", "docs", "test", "perf", "build", "ci", "style"
    };

    // Classic format: type(scope)?!?: subject. Allow optional whitespace before the colon
    // and after the optional closing scope paren, so sloppy AI output like "chore : ..." still parses.
    private static readonly Regex ConventionalSubjectRegex = new(
        @"^(?<type>" + string.Join("|", ConventionalAllowedTypes) + @")" +
        @"(?:\((?<scope>[^)]+)\))?" +
        @"(?<bang>!)?\s*:\s+" +
        @"(?<subject>.+?)\s*$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex BreakingChangeFooterRegex = new(
        @"^BREAKING CHANGE:\s*(?<reason>.+?)\s*$",
        RegexOptions.Compiled | RegexOptions.Multiline);

    // Parses a Conventional Commits subject line (and optional BREAKING CHANGE footer).
    // Returns false for messages whose first line is not a recognizable conventional subject.
    private static bool TryParseConventionalCommit(
        string message,
        out string type,
        out string? scope,
        out string subject,
        out bool breaking,
        out string? breakingFooter)
    {
        type = string.Empty;
        scope = null;
        subject = string.Empty;
        breaking = false;
        breakingFooter = null;

        if (string.IsNullOrWhiteSpace(message)) return false;

        var firstLineEnd = message.IndexOf('\n');
        var firstLine = firstLineEnd < 0 ? message : message[..firstLineEnd];

        var m = ConventionalSubjectRegex.Match(firstLine.TrimEnd());
        if (!m.Success) return false;

        type = m.Groups["type"].Value.ToLowerInvariant();
        var rawScope = m.Groups["scope"].Value;
        scope = string.IsNullOrEmpty(rawScope) ? null : rawScope.Trim();
        if (scope != null && scope.Length >= 2 && scope.StartsWith("(") && scope.EndsWith(")"))
        {
            // Defensive: the regex already forbids ')' inside the capture group, but ensure trim.
            scope = scope.Trim('(', ')');
            if (scope.Length == 0) scope = null;
        }
        subject = m.Groups["subject"].Value.Trim();
        breaking = m.Groups["bang"].Value == "!";

        var tail = firstLineEnd < 0 ? string.Empty : message[(firstLineEnd + 1)..];
        tail = tail.TrimStart('\r', '\n');
        if (!string.IsNullOrWhiteSpace(tail))
        {
            var footerMatch = BreakingChangeFooterRegex.Match(tail);
            if (footerMatch.Success)
            {
                breaking = true;
                breakingFooter = footerMatch.Groups["reason"].Value.Trim();
            }
        }

        return true;
    }

    // Builds a Conventional Commits message from its structured parts.
    // Body and breaking footer are appended with a blank-line separator when present.
    private static string FormatConventionalCommit(
        string type,
        string? scope,
        string subject,
        string body,
        string? breakingReason)
    {
        var bang = string.IsNullOrEmpty(breakingReason) ? string.Empty : "!";
        var scopePart = string.IsNullOrEmpty(scope) ? string.Empty : $"({scope})";
        var sb = new StringBuilder();
        sb.Append(type);
        sb.Append(scopePart);
        sb.Append(bang);
        sb.Append(": ");
        sb.Append(subject?.Trim() ?? string.Empty);

        var normalizedBody = (body ?? string.Empty).Trim();
        if (normalizedBody.Length > 0)
        {
            sb.Append("\n\n");
            sb.Append(normalizedBody);
        }

        if (!string.IsNullOrWhiteSpace(breakingReason))
        {
            if (normalizedBody.Length == 0) sb.Append("\n\n");
            else sb.Append("\n\n");
            sb.Append("BREAKING CHANGE: ");
            sb.Append(breakingReason.Trim());
        }

        return sb.ToString();
    }

    private static readonly Regex CsRemovedPublicRegex = new(
        @"^-.*\bpublic\s+(class|interface|struct|enum|method|void|string|int|bool|Task|async|[A-Z][A-Za-z0-9_]*\s*\()",
        RegexOptions.Compiled | RegexOptions.Multiline);

    private static readonly Regex JsRemovedExportRegex = new(
        @"^-.*\bexport\s+(function|class|const|let|var|default|async|\{|\*|[A-Z][A-Za-z0-9_]*\s*=)",
        RegexOptions.Compiled | RegexOptions.Multiline);

    private static readonly Regex JsonMajorBumpRegex = new(
        @"^\+.*""version"":\s*""(\d+)\.0\.0""",
        RegexOptions.Compiled | RegexOptions.Multiline);

    private static readonly Regex MajorBumpMarkerRegex = new(
        @"^\+.*#major\.bump",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Multiline);

    // Static-analysis pass over a unified diff. Returns one human-readable marker
    // per detection so the caller can include them in the AI prompt or as
    // breaking-change footers.
    private static IReadOnlyList<string> DetectBreakingChanges(string diff)
    {
        if (string.IsNullOrEmpty(diff)) return Array.Empty<string>();

        var markers = new List<string>();

        var csMatches = CsRemovedPublicRegex.Matches(diff);
        foreach (Match m in csMatches)
        {
            markers.Add($"csharp removed public symbol: {m.Value.TrimStart('-').Trim()}");
        }

        var jsMatches = JsRemovedExportRegex.Matches(diff);
        foreach (Match m in jsMatches)
        {
            markers.Add($"js removed export: {m.Value.TrimStart('-').Trim()}");
        }

        var jsonMatches = JsonMajorBumpRegex.Matches(diff);
        foreach (Match m in jsonMatches)
        {
            var major = m.Groups[1].Value;
            markers.Add($"json major version bump to {major}.0.0");
        }

        var markerMatches = MajorBumpMarkerRegex.Matches(diff);
        foreach (Match _ in markerMatches)
        {
            markers.Add("explicit #major.bump marker");
        }

        return markers;
    }

    // Single source of truth for "yp commit" flag parsing. Falls back to the
    // AppConfig.CommitFormat when no explicit format flag is provided.
    private static CommitArgs ParseCommitArgs(string[] args, AppConfig? config = null)
    {
        bool requireConfirmation = false;
        bool detailed = false;
        bool save = false;
        bool conventional = false;
        bool detectBreaking = false;
        bool amend = false;
        string? type = null;
        string? scope = null;
        string? templatePath = null;
        bool noSpinner = false;

        for (int i = 0; i < args.Length; i++)
        {
            var a = args[i];
            switch (a)
            {
                case "--confirm":
                    requireConfirmation = true;
                    break;
                case "--detailed":
                    detailed = true;
                    break;
                case "--save":
                    save = true;
                    break;
                case "--conventional":
                    conventional = true;
                    break;
                case "--detect-breaking":
                    detectBreaking = true;
                    break;
                case "--amend":
                    amend = true;
                    break;
                case "--type":
                    if (i + 1 < args.Length) { type = args[++i].ToLowerInvariant(); }
                    break;
                case "--scope":
                    if (i + 1 < args.Length) { scope = args[++i]; }
                    break;
                case "--template":
                    if (i + 1 < args.Length) { templatePath = args[++i]; }
                    break;
                case "--no-spinner":
                    noSpinner = true;
                    break;
                default:
                    if (a.StartsWith("--type=", StringComparison.Ordinal))
                        type = a.Substring("--type=".Length).ToLowerInvariant();
                    else if (a.StartsWith("--scope=", StringComparison.Ordinal))
                        scope = a.Substring("--scope=".Length);
                    else if (a.StartsWith("--template=", StringComparison.Ordinal))
                        templatePath = a.Substring("--template=".Length);
                    break;
            }
        }

        var language = ParseLanguage(args);

        // Resolve the effective format. Explicit flags win; otherwise honor AppConfig.CommitFormat.
        string? format = null;
        if (conventional) format = "conventional";
        else if (!string.IsNullOrEmpty(templatePath)) format = templatePath;
        else if (config?.CommitFormat is { Length: > 0 } cfgFormat) format = cfgFormat;

        return new CommitArgs
        {
            RequireConfirmation = requireConfirmation,
            Detailed = detailed,
            Save = save,
            Language = language,
            Conventional = conventional,
            Type = type,
            Scope = scope,
            DetectBreaking = detectBreaking,
            Amend = amend,
            TemplatePath = templatePath,
            Format = format,
            NoSpinner = noSpinner,
        };
    }

    // Final step of "yp commit": shape the raw AI output into the format the user requested.
    // Passthrough when no format is set. For conventional: parse → re-emit consistently
    // (forces --type/--scope if provided, prepends bang / appends footer when --detect-breaking
    // surfaces a marker). For template: load file (test ensures we don't hit it here), parse
    // prompt, render through {{vars}}. For gitmoji: passthrough (the AI prompt already
    // instructed the model).
    private static string BuildCommitMessage(string aiOutput, CommitArgs args, IReadOnlyList<string> breakingMarkers)
    {
        if (string.IsNullOrEmpty(args.Format))
        {
            return aiOutput;
        }

        // Template rendering is handled externally (caller loads the template file and
        // raises exit-6 when missing). The mid-pipeline shape is the same as conventional.
        var isTemplate = !string.Equals(args.Format, "conventional", StringComparison.OrdinalIgnoreCase)
                         && !string.Equals(args.Format, "gitmoji", StringComparison.OrdinalIgnoreCase);

        if (string.Equals(args.Format, "gitmoji", StringComparison.OrdinalIgnoreCase))
        {
            return aiOutput;
        }

        // Parse the AI output as a conventional commit (it was prompted to emit one).
        TryParseConventionalCommit(
            aiOutput,
            out var type,
            out var scope,
            out var subject,
            out var aiBreaking,
            out var aiFooter);

        // User-provided type/scope win over anything the AI produced.
        var effectiveType = !string.IsNullOrEmpty(args.Type) ? args.Type : type;
        var effectiveScope = args.Scope ?? scope;

        // Merge breaking reasons: explicit user footer wins; otherwise append detected markers.
        IReadOnlyList<string> explicitReasons = aiBreaking && !string.IsNullOrEmpty(aiFooter)
            ? new[] { aiFooter! }
            : Array.Empty<string>();

        IReadOnlyList<string> detectedReasons = args.DetectBreaking && !aiBreaking
            ? breakingMarkers
            : Array.Empty<string>();

        // Pick the strongest signal: AI footer > first detected marker.
        string? breakingReason;
        if (aiBreaking) breakingReason = aiFooter;
        else if (detectedReasons.Count > 0) breakingReason = detectedReasons[0];
        else breakingReason = null;

        var body = ExtractBody(aiOutput, subject);

        if (isTemplate)
        {
            return RenderCommitTemplate(
                LoadTemplateFileOrThrow(args.TemplatePath!, out var _),
                new Dictionary<string, string>
                {
                    ["type"] = effectiveType,
                    ["scope"] = effectiveScope ?? string.Empty,
                    ["subject"] = subject,
                    ["body"] = body,
                    // Refs parsing is handled out-of-band by the AI prompt; default to empty.
                    ["refs"] = string.Empty,
                });
        }

        return FormatConventionalCommit(
            effectiveType,
            effectiveScope,
            subject,
            body,
            breakingReason);
    }

    // Reads everything after the first line + blank line in a conventional commit.
    private static string ExtractBody(string aiOutput, string subject)
    {
        if (string.IsNullOrEmpty(aiOutput)) return string.Empty;
        var firstNewline = aiOutput.IndexOf('\n');
        if (firstNewline < 0) return string.Empty;
        var rest = aiOutput[(firstNewline + 1)..].TrimStart('\r', '\n');
        if (rest.StartsWith(subject, StringComparison.Ordinal))
        {
            // Subject happened to repeat in body — strip it.
            rest = rest[subject.Length..].TrimStart('\r', '\n');
        }
        return rest.Trim();
    }

    // Reads a template file from disk. Throws on missing file — the caller maps the
    // exception to exit code 6 per issue #11 acceptance criteria.
    private static string LoadTemplateFileOrThrow(string path, out bool _exists)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException(
                $"--template file not found: {path}", path);
        }
        _exists = true;
        return File.ReadAllText(path);
    }

    // Returns the prompt fed to the AI. Branches on the resolved commit format:
    //   - "conventional" / template / gitmoji: enforces the conventional constraint
    //     (subject matches type(scope)?!?: ...) and the BREAKING CHANGE: footer rule.
    //   - plain (no format): the legacy free-form prompt.
    // Augments the prompt with --type / --scope when explicit, and surfaces
    // detected breaking-change markers when --detect-breaking is set.
    private static string BuildCommitPrompt(CommitArgs args, string diff, IReadOnlyList<string> breakingMarkers)
    {
        var language = args.Language;
        var hasFormat = !string.IsNullOrEmpty(args.Format);

        var sb = new StringBuilder();
        sb.AppendLine("You are a git commit message expert. Based on the following git diff, generate a commit message.");
        sb.AppendLine();
        sb.Append("LANGUAGE: Write the commit message in ").Append(language).AppendLine(".");

        if (hasFormat)
        {
            sb.AppendLine();
            sb.AppendLine("FORMAT REQUIREMENTS (Conventional Commits):");
            sb.AppendLine("1. SUBJECT LINE (first line):");
            sb.AppendLine("   - MUST match the regex: type(scope)?!?: .{1,72}");
            sb.AppendLine("   - type is one of: feat, fix, docs, style, refactor, test, chore, perf, build, ci");
            sb.AppendLine("   - scope is optional, in parentheses, lowercase, no spaces");
            sb.AppendLine("   - Bang (!) BEFORE the colon marks a breaking change");
            sb.AppendLine("   - Use imperative mood: 'add' not 'added' or 'adds'");
            sb.AppendLine("   - Max 72 characters for the subject");
            sb.AppendLine();
            sb.AppendLine("2. BODY (optional):");
            sb.AppendLine("   - Blank line after the subject");
            sb.AppendLine("   - One short paragraph explaining the why; bullet points for key changes");
            sb.AppendLine("   - Lines under 72 chars");
            sb.AppendLine();
            sb.AppendLine("3. BREAKING CHANGE: footer (only when applicable):");
            sb.AppendLine("   - Append a paragraph starting with exactly 'BREAKING CHANGE: ' followed by one sentence");

            if (!string.IsNullOrEmpty(args.Type))
            {
                sb.Append("   - Use type \"").Append(args.Type).AppendLine("\"");
            }
            else
            {
                sb.AppendLine("   - Pick the most fitting type for this diff");
            }
            if (!string.IsNullOrEmpty(args.Scope))
            {
                sb.Append("   - Use scope \"").Append(args.Scope).AppendLine("\"");
            }
        }
        else
        {
            sb.AppendLine();
            sb.AppendLine("The commit message should:");
            sb.AppendLine("- Be concise but descriptive");
            sb.AppendLine("- Be in imperative mood");
            sb.AppendLine("- Not include quotes or wrappers");
        }

        if (args.DetectBreaking && breakingMarkers.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("POSSIBLE BREAKING CHANGES DETECTED IN THE DIFF:");
            foreach (var marker in breakingMarkers)
            {
                sb.Append("- ").AppendLine(marker);
            }
            sb.AppendLine("If any of the above actually represents a breaking change for our consumers, append a 'BREAKING CHANGE: <reason>' footer.");
        }

        sb.AppendLine();
        sb.AppendLine("Git diff:");
        sb.AppendLine(diff);
        sb.AppendLine();
        sb.AppendLine("Generate only the commit message (subject + optional body + optional footer):");

        return sb.ToString();
    }
}
