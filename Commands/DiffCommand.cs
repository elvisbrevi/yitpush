using Spectre.Console;
using TextCopy;

namespace YitPush;

partial class Program
{
    private static async Task<int> DiffCommand(string[] args)
    {
        bool filesOnly = args.Contains("--files");
        bool hunksOnly = args.Contains("--hunks");
        bool stat = args.Contains("--stat");
        bool json = args.Contains("--json");
        bool noColor = args.Contains("--no-color") || Console.IsOutputRedirected;

        // Positional refs: 0, 1, or 2 of them. If 2 are present we diff <refA>..<refB>.
        var refs = args.Where(a => !a.StartsWith("--", StringComparison.Ordinal)).ToArray();
        string? refA = refs.Length > 0 ? refs[0] : null;
        string? refB = refs.Length > 1 ? refs[1] : null;

        if (!await IsGitRepository())
        {
            AnsiConsole.MarkupLine("[red]❌ Error: Not a git repository.[/]");
            Console.WriteLine("\nPlease run this command from within a git repository.");
            return 1;
        }

        var diffText = await RunDiff(refA, refB, filesOnly, stat);
        if (string.IsNullOrWhiteSpace(diffText))
        {
            if (json)
            {
                Console.WriteLine("{\"files\":[]}");
            }
            else
            {
                AnsiConsole.MarkupLine("[dim]No changes.[/]");
            }
            return 0;
        }

        if (json)
        {
            var files = ParseUnifiedDiff(diffText);
            var payload = new { files = files.Select(f => new
            {
                path = f.Path,
                oldPath = f.OldPath,
                additions = f.Additions,
                deletions = f.Deletions,
                isBinary = f.IsBinary,
                isRename = f.IsRename,
                hunks = f.Hunks.Select(h => new
                {
                    beforeLine = h.BeforeLine,
                    afterLine = h.AfterLine,
                    content = h.Content
                })
            }) };
            Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(payload,
                new System.Text.Json.JsonSerializerOptions
                {
                    WriteIndented = false,
                    DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
                }));
            return 0;
        }

        if (filesOnly || stat)
        {
            RenderStat(diffText, noColor);
            return 0;
        }

        if (hunksOnly)
        {
            // Re-emit the diff with `git diff -U0` semantics: take the captured diff
            // (already -U0 if --hunks was passed) and just render it.
            RenderColored(diffText, noColor);
            return 0;
        }

        RenderColored(diffText, noColor);
        return 0;
    }

    private static async Task<string> RunDiff(string? refA, string? refB, bool filesOnly, bool stat)
    {
        // Branch-to-branch diff takes precedence.
        if (!string.IsNullOrEmpty(refA) && !string.IsNullOrEmpty(refB))
        {
            var statFlag = (filesOnly || stat) ? " --stat" : "";
            return await RunGitOutput($"diff {refA}..{refB}{statFlag}");
        }

        // Default: working tree vs index + tracked-but-unstaged, captured with the
        //   `--no-color` flag so the output is parser-friendly when piped to JSON
        //   and palette-friendly when printed to a TTY.
        var statFlag2 = (filesOnly || stat) ? " --stat" : "";
        return await RunGitOutput($"diff HEAD --no-color{statFlag2}");
    }

    private static void RenderStat(string diff, bool noColor)
    {
        // `git diff --stat` output already has the right shape: pad the path column
        // and color the +N / -M tokens when the terminal supports it.
        foreach (var rawLine in diff.Split('\n'))
        {
            var line = rawLine.TrimEnd('\r');
            if (string.IsNullOrWhiteSpace(line)) continue;
            if (noColor)
            {
                Console.WriteLine(line);
                continue;
            }

            // Lines like " src/Foo.cs | 4 ++--"
            var barIdx = line.IndexOf(" | ", StringComparison.Ordinal);
            if (barIdx < 0)
            {
                Console.WriteLine(line);
                continue;
            }
            var pathPart = line[..barIdx];
            var rest = line[(barIdx + 3)..];
            AnsiConsole.MarkupLine($"[bold]{Markup.Escape(pathPart)}[/] [dim]|[/] {Markup.Escape(rest)}");
        }
    }

    private static void RenderColored(string diff, bool noColor)
    {
        if (noColor)
        {
            Console.WriteLine(diff);
            return;
        }

        foreach (var rawLine in diff.Split('\n'))
        {
            var line = rawLine.TrimEnd('\r');
            if (line.StartsWith("+") && !line.StartsWith("+++"))
            {
                AnsiConsole.MarkupLine($"[green]{Markup.Escape(line)}[/]");
            }
            else if (line.StartsWith("-") && !line.StartsWith("---"))
            {
                AnsiConsole.MarkupLine($"[red]{Markup.Escape(line)}[/]");
            }
            else if (line.StartsWith("diff --git ", StringComparison.Ordinal)
                  || line.StartsWith("@@ ", StringComparison.Ordinal))
            {
                AnsiConsole.MarkupLine($"[bold cyan]{Markup.Escape(line)}[/]");
            }
            else
            {
                AnsiConsole.MarkupLine(Markup.Escape(line));
            }
        }
    }
}
