using System.Diagnostics;
using Spectre.Console;

namespace YitPush;

partial class Program
{
    private const string SkillPackage = "elvisbrevi/yitpush";

    private static async Task<int> InstallSkillCommand()
    {
        AnsiConsole.MarkupLine("\n[bold cyan]🧠 Installing yp agent skill...[/]");
        AnsiConsole.MarkupLine($"[dim]Running: npx skills add {SkillPackage}[/]\n");

        // Check if npx is available
        var npxCheck = await RunCommandCapture("npx", "--version");
        if (npxCheck == null)
        {
            AnsiConsole.MarkupLine("[yellow]⚠️  npx not found. Install Node.js to use the skills CLI.[/]");
            AnsiConsole.MarkupLine($"[dim]Then run manually:[/] [cyan]npx skills add {SkillPackage}[/]");
            return 1;
        }

        // Run npx skills add
        var success = await RunCommandPassthrough("npx", $"skills add {SkillPackage} --skill yp --all -y");

        if (success)
        {
            AnsiConsole.MarkupLine($"\n[green]✅ Skill installed![/] Your AI agent now knows how to use [bold]yp[/].");
            AnsiConsole.MarkupLine($"[dim]Listed at: https://skills.sh/{SkillPackage}[/]");
        }
        else
        {
            AnsiConsole.MarkupLine("\n[yellow]⚠️  Skill installation failed.[/]");
            AnsiConsole.MarkupLine($"[dim]Try manually:[/] [cyan]npx skills add {SkillPackage}[/]");
        }

        return success ? 0 : 1;
    }
}
