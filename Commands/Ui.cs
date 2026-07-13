using Spectre.Console;

namespace YitPush;

// Centralized UX helpers for long-running operations. Keeps the spinner
// decision logic in one place so the rest of the CLI can wrap any work
// with a single `await Ui.RunWithStatus("…", async ctx => { … })` call.
internal static class Ui
{
    public const string NoSpinnerEnvVar = "YITPUSH_NO_SPINNER";

    // Pure decision: should we skip the spinner wrapper?
    // Priority: explicit --no-spinner flag > env var > stdout redirected.
    public static bool ShouldDisableSpinner(bool noSpinnerFlag = false)
    {
        if (noSpinnerFlag) return true;

        var env = Environment.GetEnvironmentVariable(NoSpinnerEnvVar);
        if (!string.IsNullOrEmpty(env) &&
            (env == "1" || env.Equals("true", StringComparison.OrdinalIgnoreCase) ||
             env.Equals("yes", StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        try
        {
            if (Console.IsOutputRedirected) return true;
        }
        catch
        {
            // Some test hosts throw on IsOutputRedirected; treat as redirected.
            return true;
        }

        return false;
    }

    // Runs `work` under a Spectre.Console spinner when the environment supports
    // it. When the spinner is disabled (flag / env var / redirected stdout),
    // the title is echoed once and the work runs inline.
    //
    // The work signature takes `StatusContext?` so callers don't need to
    // null-check inside the common path; update the spinner text only when
    // a real context is provided (`ctx?.Status("…")`).
    public static async Task<T> RunWithStatus<T>(
        string title,
        Func<StatusContext?, Task<T>> work,
        bool noSpinner = false)
    {
        if (ShouldDisableSpinner(noSpinner))
        {
            AnsiConsole.MarkupLine($"[dim]{Markup.Escape(title)}[/]");
            return await work(null).ConfigureAwait(false);
        }

        return await AnsiConsole.Status()
            .StartAsync(title, async ctx => await work(ctx).ConfigureAwait(false))
            .ConfigureAwait(false);
    }
}
