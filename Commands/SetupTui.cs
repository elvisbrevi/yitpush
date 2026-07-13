using Spectre.Console;
using Spectre.Console.Rendering;

namespace YitPush;

// Interactive TUI variant of `yp setup`. Replaces the 5-step wizard with a
// two-column live layout powered by Spectre.Console's AnsiConsole.Live +
// Layout primitives. The legacy wizard stays reachable via `--wizard`
// (and is auto-selected when stdout/stdin is redirected, e.g. in CI).
//
// See issue #15 (v12) for the design and acceptance criteria.
partial class Program
{
    internal static class SetupTui
    {
        // Pure routing decision. Inlined as a static so xUnit can exercise it
        // without a real terminal (CI has no TTY, so we pass the redirect
        // flags explicitly instead of reading Console.IsInputRedirected).
        //
        // Last-flag-wins: if both --tui and --wizard are present, the most
        // recent one wins. Unknown flags are ignored — they will error out
        // later in the wizard path with a clear message.
        public static bool ShouldUseTui(
            string[] args,
            bool isInputRedirected,
            bool isOutputRedirected)
        {
            // CI safety: if either stream is piped, SelectionPrompt cannot
            // work (no real terminal) and the TUI's redraw would corrupt the
            // captured stdout. Always fall back to the wizard.
            if (isInputRedirected || isOutputRedirected) return false;

            bool useTui = true;
            foreach (var arg in args)
            {
                if (arg == "--tui") useTui = true;
                else if (arg == "--wizard") useTui = false;
            }
            return useTui;
        }

        // Convenience wrapper used by Program.cs in Main. Reads the redirect
        // flags from the actual console so the production call site stays a
        // one-liner. Wrapped in try/catch because some test hosts throw on
        // the redirect probe.
        public static bool ShouldUseTui(string[] args)
        {
            bool inputRedirected;
            bool outputRedirected;
            try
            {
                inputRedirected = Console.IsInputRedirected;
                outputRedirected = Console.IsOutputRedirected;
            }
            catch
            {
                inputRedirected = true;
                outputRedirected = true;
            }
            return ShouldUseTui(args, inputRedirected, outputRedirected);
        }

        // Entry point invoked by Program.cs when ShouldUseTui(args) is true.
        // The TUI itself cannot be exercised from xUnit, so it lives in a
        // separate method and the auto-fallback path is covered by
        // SetupTuiTests.
        public static async Task<int> RunAsync(string[] args)
        {
            // Strip the routing flags so they don't leak into the wizard if
            // the user later types --wizard and the TUI bails out for any
            // reason.
            var tuiArgs = args.Where(a => a is not "--tui" and not "--wizard").ToArray();
            return await RunTuiAsync(tuiArgs);
        }

        private static async Task<int> RunTuiAsync(string[] _)
        {
            // The visual TUI is built with AnsiConsole.Live + Layout: the
            // left column lists the providers (with a green dot for the ones
            // the user has already tested), the right column hosts the
            // configuration panel (API key + model + base URL + a "Test
            // connection" button), and a footer shows the result of the
            // last save.
            //
            // The loop runs until the user presses Enter to save or Esc to
            // cancel. Keyboard navigation:
            //   ← / →   switch provider
            //   ↑ / ↓   switch model
            //   T       test connection (uses FetchModelsForProvider)
            //   Enter   save and exit 0
            //   Esc     cancel and exit 0 (no save)
            var config = ConfigManager.Load();
            var state = new TuiState(config);

            await AnsiConsole.Live(BuildLayout(state))
                .StartAsync(async ctx =>
                {
                    while (true)
                    {
                        ctx.UpdateTarget(BuildLayout(state));
                        ctx.Refresh();

                        if (!Console.KeyAvailable)
                        {
                            await Task.Delay(50);
                            continue;
                        }

                        var key = Console.ReadKey(intercept: true);

                        switch (key.Key)
                        {
                            case ConsoleKey.LeftArrow:
                                state.MoveProvider(-1);
                                break;
                            case ConsoleKey.RightArrow:
                                state.MoveProvider(+1);
                                break;
                            case ConsoleKey.UpArrow:
                                state.MoveModel(-1);
                                break;
                            case ConsoleKey.DownArrow:
                                state.MoveModel(+1);
                                break;
                            case ConsoleKey.T:
                                state.Status = "Testing connection…";
                                ctx.Refresh();
                                await TestConnectionAsync(state);
                                break;
                            case ConsoleKey.Enter:
                                Save(state);
                                state.Status = "Saved ✅";
                                ctx.Refresh();
                                return;
                            case ConsoleKey.Escape:
                                state.Status = "Cancelled";
                                ctx.Refresh();
                                return;
                        }
                    }
                });

            return 0;
        }

        private static void Save(TuiState state)
        {
            var config = ConfigManager.Load();
            config.DefaultProvider = state.SelectedProviderKey;
            if (!config.Providers.ContainsKey(state.SelectedProviderKey))
                config.Providers[state.SelectedProviderKey] = new ProviderConfig();

            config.Providers[state.SelectedProviderKey].ApiKey = state.ApiKey;
            config.Providers[state.SelectedProviderKey].Model = state.SelectedModel;
            config.Providers[state.SelectedProviderKey].IsActive = true;
            if (!string.IsNullOrEmpty(state.CustomBaseUrl))
                config.Providers[state.SelectedProviderKey].BaseUrl = state.CustomBaseUrl;

            ConfigManager.Save(config);
        }

        private static async Task TestConnectionAsync(TuiState state)
        {
            try
            {
                var models = await FetchModelsForProvider(
                    state.SelectedProviderKey, state.ApiKey, state.CustomBaseUrl);
                if (models.Count > 0)
                {
                    state.Models = models;
                    state.SelectedModelIndex = 0;
                    state.TestedProviders[state.SelectedProviderKey] = true;
                    state.Status = $"Found {models.Count} models ✅";
                }
                else
                {
                    state.TestedProviders[state.SelectedProviderKey] = false;
                    state.Models = GetDefaultModelsForProvider(state.SelectedProvider);
                    state.SelectedModelIndex = 0;
                    state.Status = "Could not fetch live models — using defaults";
                }
            }
            catch
            {
                state.TestedProviders[state.SelectedProviderKey] = false;
                state.Status = "Connection failed ❌";
            }
        }

        private static Layout BuildLayout(TuiState state)
        {
            var left = new Panel(BuildProviderList(state))
                .Header("[bold cyan]Provider[/]")
                .BorderColor(state.SelectedProviderIsTested ? Color.Green : Color.Grey);

            var right = new Panel(BuildConfigPanel(state))
                .Header($"[bold cyan]{state.SelectedProvider} — Configuration[/]")
                .BorderColor(Color.Grey);

            var footer = new Panel(
                    new Markup(
                        $"[dim]←/→ provider   ↑/↓ model   T test   Enter save   Esc cancel[/]\n" +
                        $"[yellow]{Markup.Escape(state.Status)}[/]"))
                .Border(BoxBorder.None);

            return new Layout("root")
                .SplitRows(
                    new Layout("body").Ratio(8)
                        .SplitColumns(
                            new Layout("left").Ratio(1),
                            new Layout("right").Ratio(2)),
                    new Layout("footer").Size(3))
                .GetLayout("left").Update(left)
                .GetLayout("right").Update(right)
                .GetLayout("footer").Update(footer);
        }

        private static IRenderable BuildProviderList(TuiState state)
        {
            var grid = new Grid().AddColumn(new GridColumn().NoWrap());
            for (int i = 0; i < state.Providers.Count; i++)
            {
                var p = state.Providers[i];
                var marker = i == state.SelectedProviderIndex ? "[cyan]▶[/]" : " ";
                var tested = state.TestedProviders.TryGetValue(p.Key, out var ok) && ok
                    ? "[green]●[/]"
                    : " ";
                var label = i == state.SelectedProviderIndex
                    ? $"[cyan]{Markup.Escape(p.Display)}[/]"
                    : Markup.Escape(p.Display);
                grid.AddRow($"{marker} {tested} {label}");
            }
            return grid;
        }

        private static IRenderable BuildConfigPanel(TuiState state)
        {
            var key = string.IsNullOrEmpty(state.ApiKey) ? "[dim](not set — set the env var or use --wizard)[/]" : "[green](set)[/]";
            var baseUrl = string.IsNullOrEmpty(state.CustomBaseUrl)
                ? state.DefaultBaseUrl
                : state.CustomBaseUrl;

            var info = new Markup(
                $"[bold]API key:[/] {key}\n" +
                $"[bold]Endpoint:[/] {Markup.Escape(baseUrl)}\n" +
                $"[bold]Status:[/] {Markup.Escape(state.Status)}\n");

            var modelTable = new Table()
                .Border(TableBorder.Rounded)
                .BorderColor(Color.Grey)
                .Title("[bold]Available models[/]")
                .AddColumn("[bold]Model[/]");

            if (state.Models.Count == 0)
            {
                modelTable.AddRow("[dim](press T to test the connection)[/]");
            }
            else
            {
                for (int i = 0; i < state.Models.Count; i++)
                {
                    var m = state.Models[i];
                    var marker = i == state.SelectedModelIndex ? "[cyan]▶[/]" : " ";
                    modelTable.AddRow($"{marker} {Markup.Escape(m)}");
                }
            }

            return new Rows(info, modelTable);
        }

        // Mutable per-session state for the TUI. Kept private + nested so
        // the surface stays minimal and the tests can poke at the routing
        // decision without instantiating one.
        private sealed class TuiState
        {
            public readonly List<(string Key, string Display)> Providers = new()
            {
                ("OpenAI", "OpenAI"),
                ("Anthropic", "Anthropic"),
                ("Google", "Google Gemini"),
                ("DeepSeek", "DeepSeek"),
                ("OpenRouter", "OpenRouter"),
                ("Nvidia", "NVIDIA NIM"),
            };

            public Dictionary<string, bool> TestedProviders { get; } = new();
            public List<string> Models { get; set; } = new();
            public int SelectedProviderIndex { get; set; }
            public int SelectedModelIndex { get; set; } = -1;
            public string ApiKey { get; set; } = string.Empty;
            public string CustomBaseUrl { get; set; } = string.Empty;
            public string Status { get; set; } = "Ready";

            public TuiState(AppConfig config)
            {
                if (!string.IsNullOrEmpty(config.DefaultProvider))
                {
                    var idx = Providers.FindIndex(p => p.Key == config.DefaultProvider);
                    if (idx >= 0) SelectedProviderIndex = idx;
                }

                // Pre-populate the API key from the env var if any (the TUI
                // cannot collect secrets in a safe way without a proper
                // text input field; rely on the env var convention used by
                // every other command in the tool).
                var envVar = config.DefaultProvider?.ToUpperInvariant().Replace(" ", "_") + "_API_KEY";
                if (!string.IsNullOrEmpty(envVar))
                {
                    var envKey = Environment.GetEnvironmentVariable(envVar);
                    if (!string.IsNullOrEmpty(envKey)) ApiKey = envKey;
                }
            }

            public string SelectedProvider => Providers[SelectedProviderIndex].Display;
            public string SelectedProviderKey => Providers[SelectedProviderIndex].Key;
            public string SelectedModel =>
                SelectedModelIndex >= 0 && SelectedModelIndex < Models.Count
                    ? Models[SelectedModelIndex]
                    : string.Empty;
            public bool SelectedProviderIsTested =>
                TestedProviders.TryGetValue(SelectedProviderKey, out var v) && v;

            public string DefaultBaseUrl => SelectedProviderKey switch
            {
                "OpenAI" => "https://api.openai.com/v1/chat/completions",
                "Anthropic" => "https://api.anthropic.com/v1/messages",
                "Google" => "https://generativelanguage.googleapis.com/v1beta",
                "DeepSeek" => "https://api.deepseek.com/v1/chat/completions",
                "OpenRouter" => "https://openrouter.ai/api/v1/chat/completions",
                "Nvidia" => "https://integrate.api.nvidia.com/v1/chat/completions",
                _ => "https://api.openai.com/v1/chat/completions"
            };

            public void MoveProvider(int delta)
            {
                SelectedProviderIndex = (SelectedProviderIndex + delta + Providers.Count) % Providers.Count;
                SelectedModelIndex = -1;
            }

            public void MoveModel(int delta)
            {
                if (Models.Count == 0) return;
                if (SelectedModelIndex < 0) SelectedModelIndex = 0;
                else SelectedModelIndex = (SelectedModelIndex + delta + Models.Count) % Models.Count;
            }
        }
    }
}
