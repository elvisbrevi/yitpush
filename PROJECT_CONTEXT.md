# PROJECT_CONTEXT.md — YitPush (yp)

> This file provides deep architectural context for Claude Projects and custom AI agents working on this codebase.

---

## What is this?

`yp` (YitPush) is a .NET 10 global CLI tool that combines:
1. **AI-powered Git automation** — generates commit messages and PR descriptions using configurable AI providers
2. **Azure DevOps integration** — manages user stories, tasks, repositories, variable groups, and work item links via the Azure CLI and REST API

It is installed as a dotnet global tool (`dotnet tool install -g YitPush`) and invoked with `yp`.

---

## Key Design Decisions

### Single `partial class Program`
The entire application is one C# class (`partial class Program`) split across multiple files for readability. There are no separate service classes, DI containers, or interfaces — this is intentional to keep the tool simple and self-contained. Do not introduce DI or abstract interfaces unless there is a very strong reason.

### Spectre.Console for all UI
Every piece of user-facing output uses `Spectre.Console` (`AnsiConsole`). Raw `Console.Write` is only used in a handful of encoding-setup lines. All interactive menus use `SelectionPrompt<string>` with a `← Back` option (`BackOption` constant) that allows navigation without exiting.

### AI Providers
Providers are dispatched in `CallAiApi()` inside `Providers/AiProviders.cs`. OpenAI, DeepSeek, and OpenRouter share `CallOpenAiCompatibleApi()`. Anthropic and Google Gemini have their own methods due to different API formats. The active provider is loaded from `~/.yitpush/config.json` via `ConfigManager`, with environment variable overrides as a fallback.

### Azure DevOps via Azure CLI + REST
Azure DevOps operations use two approaches:
- `az` CLI commands (via `RunAzCapture`) for authentication, repo management, and variable groups
- Direct REST API calls (via `HttpClient`) for work item operations that the CLI doesn't support well

### Backward Compatibility
If no `~/.yitpush/config.json` exists, the tool falls back to `DEEPSEEK_API_KEY` env var. This ensures existing users aren't broken by the multi-provider migration.

---

## File Responsibilities

| File | Responsibility |
|------|---------------|
| `Program.cs` | `Main()`, routing, `ShowHelp()`, `CheckForUpdates()`, all shared constants |
| `Models.cs` | All data model classes (`AppConfig`, `ProviderConfig`, `DeepSeekResponse`, `VersionCheckCache`, etc.) |
| `Commands/CommitCommand.cs` | `yp commit` — git diff → AI prompt → commit message → git add/commit/push |
| `Commands/PrCommand.cs` | `yp pr` — branch selection → git diff → AI prompt → PR description |
| `Commands/SetupCommand.cs` | `yp setup` — provider selection → API key → model → validate → save + alias install |
| `Commands/CheckoutCommand.cs` | `yp checkout` — interactive branch switcher |
| `Providers/AiProviders.cs` | `ConfigManager`, `GetAiInfo()`, `CallAiApi()`, `CallOpenAiCompatibleApi()`, `CallAnthropicApi()`, `CallGeminiApi()` |
| `Git/GitHelpers.cs` | `ParseLanguage()`, `IsGitRepository()`, `GetGitDiff()`, `ExecuteGitPush()`, `RunGitOutput()`, `GetGitBranches()`, etc. |
| `AzureDevOps/AzureDevOpsCommand.cs` | `AzureDevOpsCommand()` routing, interactive menu, `ShowAzureDevOpsHelp()` |
| `AzureDevOps/AzureDevOpsHelpers.cs` | All Azure REST API calls, work item CRUD, task creation, link management, auth helpers |

---

## Important Constants

Defined at the top of `Program.cs`, shared across all partial files:

```csharp
ApiMaxTokens = 8000           // max_tokens for all AI calls
ApiTimeoutSeconds = 120       // HttpClient timeout
ApiMaxContextTokens = 131072  // context window for prompt truncation

AzFieldEffortHH      = "Custom.EsfuerzoEstimadoHH"
AzFieldEffortRealHH  = "Custom.EsfuerzoRealHH"
AzFieldRemainingWork = "Microsoft.VSTS.Scheduling.RemainingWork"
```

---

## Config & State Files

| Path | Purpose |
|------|---------|
| `~/.yitpush/config.json` | Active AI provider, API key, model |
| `~/.yitpush/version-check.json` | Cache for NuGet version check (refreshed every 24h) |

---

## Patterns Used Throughout

### Interactive menu with Back
```csharp
var choices = new List<string>(items) { BackOption };
var selected = AnsiConsole.Prompt(
    new SelectionPrompt<string>()
        .Title("Select:")
        .PageSize(15)
        .HighlightStyle(new Style(Color.Cyan1))
        .AddChoices(choices));
if (selected == BackOption) return 0;
```

### Simple git command output
```csharp
var result = await RunGitOutput("branch --show-current");
```

### AI call (provider-agnostic)
```csharp
var response = await CallAiApi(prompt);
```

### Error display
```csharp
AnsiConsole.MarkupLine("[red]❌ Error message here.[/]");
```

---

## Adding a New Command

1. Create `Commands/MyCommand.cs` with `namespace YitPush; partial class Program { private static async Task<int> MyCommand(...) { ... } }`
2. Add `case "my-command": result = await MyCommand(args.Skip(1).ToArray()); break;` to the switch in `Program.cs`
3. Add a row to the commands table in `ShowHelp()` in `Program.cs`

## Adding a New AI Provider

1. Add base URL to `GetDefaultBaseUrl()` in `Providers/AiProviders.cs`
2. Add model list to `GetDefaultModelsForProvider()` in `Commands/SetupCommand.cs`
3. If OpenAI-compatible: add a case in `CallAiApi()` routing to `CallOpenAiCompatibleApi()`
4. If unique API: implement `Call<Name>Api()` and add a case in `CallAiApi()`

---

## Dependencies

| Package | Purpose |
|---------|---------|
| `Spectre.Console` 0.54.0 | All interactive UI (tables, prompts, panels, colors) |
| `TextCopy` 6.2.1 | Copy generated text to clipboard |
| .NET 10 BCL | `HttpClient`, `JsonSerializer`, `Process`, `File` |
