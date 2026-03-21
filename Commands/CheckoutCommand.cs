using Spectre.Console;

namespace YitPush;

partial class Program
{
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
}
