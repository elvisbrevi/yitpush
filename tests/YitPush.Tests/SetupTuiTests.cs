using Spectre.Console;
using YitPush;

namespace YitPush.Tests;

// Covers the auto-fallback decision for the new TUI mode of `yp setup`.
// The TUI itself is interactive and cannot be exercised from xUnit (CI has no
// real terminal), so the only behavior we can pin down is the routing rule:
//
//   - explicit `--wizard`       -> wizard (no TUI)
//   - explicit `--tui`          -> TUI    (no wizard)
//   - stdin/stdout redirected   -> wizard (CI safety)
//   - default                   -> TUI    (interactive terminal)
//
// The redirect check uses Console.IsInputRedirected / IsOutputRedirected,
// which is what Spectre.Console would itself bail out on, so we treat the
// call as `isInputRedirected: false, isOutputRedirected: false` in tests.
public class SetupTuiTests
{
    [Fact]
    public void ShouldUseTui_returns_true_when_no_flags_and_terminal_is_interactive()
    {
        Assert.True(Program.SetupTui.ShouldUseTui(
            args: Array.Empty<string>(),
            isInputRedirected: false,
            isOutputRedirected: false));
    }

    [Fact]
    public void ShouldUseTui_returns_true_when_explicit_tui_flag()
    {
        Assert.True(Program.SetupTui.ShouldUseTui(
            args: new[] { "--tui" },
            isInputRedirected: false,
            isOutputRedirected: false));
    }

    [Fact]
    public void ShouldUseTui_returns_false_when_explicit_wizard_flag()
    {
        Assert.False(Program.SetupTui.ShouldUseTui(
            args: new[] { "--wizard" },
            isInputRedirected: false,
            isOutputRedirected: false));
    }

    [Fact]
    public void ShouldUseTui_returns_false_when_stdin_is_redirected_even_with_tui_flag()
    {
        // CI safety: if stdin is piped in, we cannot use SelectionPrompt
        // because there is no real terminal to read from. Wizard is the
        // safe default, and an explicit --wizard is unnecessary.
        Assert.False(Program.SetupTui.ShouldUseTui(
            args: new[] { "--tui" },
            isInputRedirected: true,
            isOutputRedirected: false));
    }

    [Fact]
    public void ShouldUseTui_returns_false_when_stdout_is_redirected()
    {
        // When stdout is piped (e.g. `yp setup > log.txt`), the TUI would
        // also break Spectre's cursor positioning, so we fall back to the
        // legacy wizard.
        Assert.False(Program.SetupTui.ShouldUseTui(
            args: Array.Empty<string>(),
            isInputRedirected: false,
            isOutputRedirected: true));
    }

    [Fact]
    public void ShouldUseTui_ignores_unrelated_args()
    {
        // Unknown flags should not flip the decision; the wizard is the
        // safe path for unknown args so the user gets a clear error.
        Assert.True(Program.SetupTui.ShouldUseTui(
            args: new[] { "--provider", "openai" },
            isInputRedirected: false,
            isOutputRedirected: false));
    }

    [Fact]
    public void ShouldUseTui_wizard_flag_wins_over_tui_flag_when_both_present()
    {
        // Last-flag-wins convention: the user typed --wizard after --tui
        // explicitly to opt out, so honor the most recent intent.
        Assert.False(Program.SetupTui.ShouldUseTui(
            args: new[] { "--tui", "--wizard" },
            isInputRedirected: false,
            isOutputRedirected: false));
    }
}
