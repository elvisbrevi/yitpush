using Spectre.Console;
using TextCopy;

namespace YitPush;

partial class Program
{
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

    private static async Task<int> GeneratePrDescription(bool detailed, string language, bool save)
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
        AnsiConsole.MarkupLine($"🤖 Generating PR description with {aiProviderName} ({aiModel})...{(detailed ? " (detailed mode)" : "")}");
        var description = await GeneratePrDescriptionContent(diff, detailed, language);

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
}
