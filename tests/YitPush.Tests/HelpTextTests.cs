using Spectre.Console;
using Spectre.Console.Rendering;
using YitPush;

namespace YitPush.Tests;

public class HelpTextTests
{
    [Fact]
    public void BuildHelpText_mentions_task_delete_subcommand()
    {
        var help = Program.BuildHelpText();
        Assert.Contains("task delete", help);
    }

    [Fact]
    public void BuildHelpText_mentions_task_attach_subcommand()
    {
        var help = Program.BuildHelpText();
        Assert.Contains("task attach", help);
    }

    [Fact]
    public void BuildHelpText_mentions_resolve_field_subcommand()
    {
        var help = Program.BuildHelpText();
        Assert.Contains("resolve-field", help);
    }

    [Fact]
    public void BuildHelpText_mentions_refresh_fields_subcommand()
    {
        var help = Program.BuildHelpText();
        Assert.Contains("refresh-fields", help);
    }

    [Fact]
    public void BuildHelpText_mentions_diff_command()
    {
        var help = Program.BuildHelpText();
        Assert.Contains("diff", help);
    }

    [Fact]
    public void BuildHelpText_mentions_pr_list_subcommand()
    {
        var help = Program.BuildHelpText();
        Assert.Contains("pr list", help);
    }

    [Fact]
    public void BuildHelpText_mentions_pr_show_subcommand()
    {
        var help = Program.BuildHelpText();
        Assert.Contains("pr show", help);
    }

    [Fact]
    public void BuildHelpText_mentions_pr_comments_subcommand()
    {
        var help = Program.BuildHelpText();
        Assert.Contains("pr comments", help);
    }

    [Fact]
    public void BuildHelpText_mentions_pr_reply_subcommand()
    {
        var help = Program.BuildHelpText();
        Assert.Contains("pr reply", help);
    }

    [Fact]
    public void BuildHelpText_mentions_pr_create_subcommand()
    {
        var help = Program.BuildHelpText();
        Assert.Contains("pr create", help);
    }

    [Fact]
    public void BuildHelpText_mentions_diff_top_level_command()
    {
        var help = Program.BuildHelpText();
        Assert.Contains("diff", help);
    }

    [Theory]
    [InlineData("task delete")]
    [InlineData("task attach")]
    [InlineData("resolve-field")]
    [InlineData("refresh-fields")]
    [InlineData("pr list")]
    [InlineData("pr show")]
    [InlineData("pr comments")]
    [InlineData("pr reply")]
    [InlineData("pr create")]
    public void ShowHelp_mentions_subcommand(string subcommand)
    {
        var rendered = CaptureShowHelp();
        Assert.Contains(subcommand, rendered);
    }

    [Fact]
    public void ShowHelp_mentions_diff_top_level_command()
    {
        var rendered = CaptureShowHelp();
        Assert.Contains("yp diff", rendered);
    }

    private static string CaptureShowHelp()
    {
        var writer = new StringWriter();
        var original = AnsiConsole.Console;
        AnsiConsole.Console = AnsiConsole.Create(new AnsiConsoleSettings
        {
            Out = new AnsiConsoleOutput(writer),
            Ansi = AnsiSupport.No,
            ColorSystem = ColorSystemSupport.NoColors,
            Interactive = InteractionSupport.No,
        });
        try
        {
            Program.ShowHelp();
        }
        finally
        {
            AnsiConsole.Console = original;
        }
        var text = writer.ToString();
        return StripAnsi(text);
    }

    private static string StripAnsi(string s)
    {
        var sb = new System.Text.StringBuilder(s.Length);
        for (var i = 0; i < s.Length; i++)
        {
            var c = s[i];
            if (c == '\u001b' || c == '\u009b')
            {
                while (i + 1 < s.Length && !char.IsLetter(s[i + 1])) i++;
                if (i + 1 < s.Length) i++;
                continue;
            }
            sb.Append(c);
        }
        return sb.ToString();
    }
}
