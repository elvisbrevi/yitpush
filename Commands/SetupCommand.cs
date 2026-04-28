using System.Runtime.InteropServices;
using Spectre.Console;

namespace YitPush;

partial class Program
{
    private static async Task<int> SetupCommand()
    {
        AnsiConsole.MarkupLine("[bold cyan]🔧 YitPush (yp) Setup[/]\n");
        AnsiConsole.MarkupLine("Configure your AI provider for commit message and PR description generation.\n");

        var config = ConfigManager.Load();
        bool hasExistingConfig = !string.IsNullOrEmpty(config.DefaultProvider);

        if (hasExistingConfig)
        {
            AnsiConsole.MarkupLine($"[yellow]⚠️  Existing configuration detected. Current provider: [bold]{config.DefaultProvider}[/], Model: [bold]{config.Providers[config.DefaultProvider].Model}[/][/]");
            var overwrite = AnsiConsole.Prompt(
                new ConfirmationPrompt("Do you want to reconfigure?") { DefaultValue = false });
            if (!overwrite)
            {
                AnsiConsole.MarkupLine("[green]✅ Configuration unchanged.[/]");
                return 0;
            }
        }

        // Step 1: Select provider
        var providerChoices = new[] { "OpenAI", "Anthropic", "Google Gemini", "DeepSeek", "OpenRouter" };
        var selectedProvider = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("📡 Select your AI provider:")
                .HighlightStyle(new Style(Color.Cyan1))
                .AddChoices(providerChoices));

        string providerKey = selectedProvider == "Google Gemini" ? "Google" : selectedProvider;

        // Step 2: Enter API key
        var apiKey = AnsiConsole.Prompt(
            new TextPrompt<string>($"🔑 Enter your [cyan]{selectedProvider}[/] API key:")
                .PromptStyle("green")
                .Secret());

        // Step 3: Custom base URL (for OpenRouter)
        string? customBaseUrl = null;
        if (selectedProvider == "OpenRouter")
        {
            AnsiConsole.MarkupLine($"[dim]Default endpoint: {GetDefaultBaseUrl("OpenRouter")}[/]");
            var useCustomUrl = AnsiConsole.Prompt(
                new ConfirmationPrompt("Use a custom base URL?") { DefaultValue = false });
            if (useCustomUrl)
            {
                customBaseUrl = AnsiConsole.Prompt(
                    new TextPrompt<string>("Enter custom base URL (full chat completions endpoint):"));
            }
        }

        // Step 4: Select model — try live fetch from the provider, fall back to a curated static list
        List<string> models = new();
        bool fromLiveApi = false;
        await AnsiConsole.Status()
            .Spinner(Spinner.Known.Dots)
            .StartAsync($"Fetching available models from {selectedProvider}...", async _ =>
            {
                models = await FetchModelsForProvider(providerKey, apiKey, customBaseUrl);
            });

        if (models.Count > 0)
        {
            fromLiveApi = true;
            AnsiConsole.MarkupLine($"[green]✅ Found {models.Count} models from {selectedProvider} API.[/]");
        }
        else
        {
            models = GetDefaultModelsForProvider(selectedProvider);
            AnsiConsole.MarkupLine($"[yellow]⚠️  Could not fetch live model list. Using built-in defaults.[/]");
        }

        const string customOption = "[Custom...]";
        var escapedCustomOption = Markup.Escape(customOption);

        // Escape model names for Spectre.Console markup (model names from live APIs may contain [ ] < > &)
        var escapedModels = models.ConvertAll(Markup.Escape);
        escapedModels.Add(escapedCustomOption);

        var modelPrompt = new SelectionPrompt<string>()
            .Title($"🧠 Select model for [cyan]{selectedProvider}[/]" + (fromLiveApi ? " [dim](live)[/]" : " [dim](defaults)[/]") + ":")
            .PageSize(15)
            .MoreChoicesText("[grey](Move up and down to see more models)[/]")
            .HighlightStyle(new Style(Color.Cyan1))
            .AddChoices(escapedModels);

        var selectedModel = AnsiConsole.Prompt(modelPrompt);

        if (selectedModel == escapedCustomOption)
        {
            selectedModel = AnsiConsole.Prompt(
                new TextPrompt<string>("Enter model name:"));
        }
        else
        {
            // Unescape the selected model name back to its original form
            selectedModel = selectedModel
                .Replace("[[", "[")
                .Replace("[]", "]")
                .Replace("<<", "<")
                .Replace(">>", ">")
                .Replace("&&", "&");
        }

        // Step 5: Validate API key
        AnsiConsole.MarkupLine("\n[dim]Testing API connection...[/]");
        bool isValid = await ValidateApiKey(providerKey, apiKey, selectedModel, customBaseUrl);
        if (!isValid)
        {
            AnsiConsole.MarkupLine("[yellow]⚠️  Could not validate API key. Saving anyway — please verify your key is correct.[/]");
        }
        else
        {
            AnsiConsole.MarkupLine("[green]✅ API key validated successfully.[/]");
        }

        // Save config
        config.DefaultProvider = providerKey;
        if (!config.Providers.ContainsKey(providerKey))
            config.Providers[providerKey] = new ProviderConfig();

        config.Providers[providerKey].ApiKey = apiKey;
        config.Providers[providerKey].Model = selectedModel;
        config.Providers[providerKey].IsActive = true;
        if (customBaseUrl != null)
            config.Providers[providerKey].BaseUrl = customBaseUrl;

        ConfigManager.Save(config);
        AnsiConsole.MarkupLine($"\n[green]✅ Configuration saved![/] Provider: [bold]{selectedProvider}[/], Model: [bold]{selectedModel}[/]");
        AnsiConsole.MarkupLine("[dim]Config stored at: ~/.yitpush/config.json[/]");

        // Offer to install the yitpush alias
        InstallAlias();

        // Offer to install the agent skill
        AnsiConsole.WriteLine();
        var installSkill = AnsiConsole.Prompt(
            new ConfirmationPrompt("Install the yp skill for your AI agent? (Claude Code, Cursor, Gemini CLI, etc.)")
            { DefaultValue = true });

        if (installSkill)
        {
            await InstallSkillCommand();
        }
        else
        {
            AnsiConsole.MarkupLine("\n[dim]You can install the skill later with:[/] [cyan]yp skill[/]");
        }

        Console.WriteLine();
        return 0;
    }

    private static void InstallAlias()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            AnsiConsole.MarkupLine("\n[dim]ℹ️  Windows detected. To use 'yitpush' as an alias for 'yp', add this to your PowerShell profile:[/]");
            AnsiConsole.MarkupLine("[dim]   Set-Alias yitpush yp[/]");
            return;
        }

        // Detect shell rc file
        var shell = Environment.GetEnvironmentVariable("SHELL") ?? string.Empty;
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        string rcFile;
        if (shell.EndsWith("zsh"))
            rcFile = Path.Combine(home, ".zshrc");
        else if (shell.EndsWith("bash"))
            rcFile = Path.Combine(home, ".bashrc");
        else
            rcFile = Path.Combine(home, ".profile");

        const string aliasLine = "alias yitpush='yp'";

        // Check if alias already exists
        if (File.Exists(rcFile))
        {
            var existing = File.ReadAllText(rcFile);
            if (existing.Contains(aliasLine))
            {
                AnsiConsole.MarkupLine($"\n[green]✅ Alias 'yitpush' already configured in {rcFile}[/]");
                return;
            }
        }

        AnsiConsole.MarkupLine($"\n[dim]Shell config detected: {rcFile}[/]");
        var addAlias = AnsiConsole.Prompt(
            new ConfirmationPrompt("Add 'yitpush' as an alias for 'yp' to your shell config?")
            { DefaultValue = true });

        if (!addAlias) return;

        File.AppendAllText(rcFile, $"\n# yitpush alias (added by yp setup)\n{aliasLine}\n");
        AnsiConsole.MarkupLine($"[green]✅ Alias added to {rcFile}[/]");
        AnsiConsole.MarkupLine($"[dim]Run 'source {rcFile}' or open a new terminal to activate it.[/]");
    }

    private static List<string> GetDefaultModelsForProvider(string provider) => provider switch
    {
        "OpenAI" => new List<string> { "gpt-4o", "gpt-4o-mini", "gpt-4-turbo", "o1", "o1-mini" },
        "Anthropic" => new List<string> { "claude-opus-4-6", "claude-sonnet-4-6", "claude-haiku-4-5-20251001" },
        "Google Gemini" => new List<string> { "gemini-2.0-flash", "gemini-2.0-pro-exp-02-05", "gemini-1.5-pro", "gemini-1.5-flash" },
        "DeepSeek" => new List<string> { "deepseek-chat", "deepseek-reasoner" },
        "OpenRouter" => new List<string> { "google/gemini-2.0-flash-exp:free", "anthropic/claude-sonnet-4-6", "openai/gpt-4o", "deepseek/deepseek-chat", "meta-llama/llama-3.3-70b-instruct:free", "qwen/qwen-2.5-72b-instruct:free" },
        _ => new List<string> { "gpt-4o" }
    };

    private static async Task<bool> ValidateApiKey(string providerKey, string apiKey, string model, string? baseUrl)
    {
        try
        {
            var testPrompt = "Reply with exactly one word: ok";
            string result = providerKey switch
            {
                "Anthropic" => await CallAnthropicApi(apiKey, model, testPrompt),
                "Google" => await CallGeminiApi(apiKey, model, testPrompt),
                _ => await CallOpenAiCompatibleApi(apiKey, model, baseUrl ?? GetDefaultBaseUrl(providerKey), testPrompt)
            };
            return !string.IsNullOrEmpty(result);
        }
        catch
        {
            return false;
        }
    }
}
