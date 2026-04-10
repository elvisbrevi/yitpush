using Spectre.Console;
using TextCopy;

namespace YitPush;

partial class Program
{
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

            var (aiApiKey, aiModel, aiProviderName, _) = GetAiInfo();
            if (string.IsNullOrEmpty(aiApiKey))
            {
                AnsiConsole.MarkupLine("[red]❌ No AI provider configured.[/]");
                AnsiConsole.MarkupLine("Run [cyan]yp setup[/] to configure your AI provider.");
                AnsiConsole.MarkupLine("[dim]Or set DEEPSEEK_API_KEY environment variable for DeepSeek (backward compatible).[/]");
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
                Console.WriteLine($"🤖 Generating commit message with {aiProviderName} ({aiModel})...{(detailed ? " (detailed mode)" : "")}");
                commitMessage = await GenerateCommitMessage(diff, detailed, language);

                if (string.IsNullOrWhiteSpace(commitMessage))
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("❌ Error: Failed to generate commit message.");
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
                if (!await ExecuteGitCommitWithMessage(commitMessage!))
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

    private static async Task<string> GenerateCommitMessage(string diff, bool detailed = false, string language = "english")
    {
        const int reservedTokens = ApiMaxTokens + ApiMaxTokens; // extra buffer for system prompt + formatting
        const int maxPromptTokens = ApiMaxContextTokens - reservedTokens;
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

        return await CallAiApi(prompt);
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
}
