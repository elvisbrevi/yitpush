# CLAUDE.md

Guidance for Claude Code (claude.ai/code) working in this repository.

## Project

`yp` (YitPush) is a .NET 10 global CLI tool packaged as a `dotnet tool` (`PackAsTool=true`, `ToolCommandName=yp`). It does two things:

1. **AI-powered Git automation** — `yp commit`, `yp pr`, `yp checkout`
2. **Azure DevOps management** — `yp azure-devops ...` (repos, user stories, tasks, links)

AI calls go through one of five providers (OpenAI, Anthropic, Google Gemini, DeepSeek, OpenRouter) configured via `yp setup` and stored in `~/.yitpush/config.json`.

## Build, run, package

```bash
dotnet build                                    # compile
dotnet run -- <command> [args]                  # run from source (e.g. dotnet run -- commit --confirm)
dotnet pack -c Release                          # produce .nupkg in ./nupkg
dotnet tool install --global --add-source ./nupkg YitPush   # install locally
dotnet tool update -g YitPush                   # update from NuGet
dotnet tool uninstall -g YitPush                # uninstall
```

There are no automated tests in this repo. Verify changes by running the CLI against a scratch git repo and an Azure DevOps test project.

When bumping the version, edit `<Version>` in `YitPush.csproj` and add an entry to `CHANGELOG.md`.

## Architecture

Single `partial class Program` split across files by feature. **No DI, no interfaces, no service classes** — this is intentional. Don't introduce them.

```
Program.cs                       Main(), routing switch, ShowHelp(), CheckForUpdates(), shared constants
Models.cs                        All data model classes (AppConfig, ProviderConfig, DeepSeekResponse, ...)
Commands/CommitCommand.cs        yp commit
Commands/PrCommand.cs            yp pr
Commands/SetupCommand.cs         yp setup (provider/key/model wizard + alias installer)
Commands/CheckoutCommand.cs      yp checkout
Commands/SkillCommand.cs         yp skill (runs `npx skills add elvisbrevi/yitpush`)
Providers/AiProviders.cs         ConfigManager, GetAiInfo(), CallAiApi() + per-provider HTTP
Git/GitHelpers.cs                ParseLanguage, IsGitRepository, GetGitDiff, RunGitOutput, branch helpers
AzureDevOps/AzureDevOpsCommand.cs Routing, interactive menu, ShowAzureDevOpsHelp
AzureDevOps/AzureDevOpsHelpers.cs Azure REST API calls, work item CRUD, link management
```

Adding a new top-level command: create `Commands/MyCommand.cs` as `namespace YitPush; partial class Program { private static async Task<int> MyCommand(...) { ... } }`, add a `case` in the switch in `Program.cs`, then add a row to the table in `ShowHelp()`.

Adding a new AI provider: extend `GetDefaultBaseUrl()` in `Providers/AiProviders.cs`, add a model list to `GetDefaultModelsForProvider()` in `Commands/SetupCommand.cs`, then either route to `CallOpenAiCompatibleApi()` (if OpenAI-shaped) or implement `Call<Name>Api()` and dispatch from `CallAiApi()`.

## Constants (defined in Program.cs)

Always reference these — never hardcode:

```csharp
ApiMaxTokens = 8000
ApiTimeoutSeconds = 120
ApiMaxContextTokens = 131072
BackOption = "← Back"
AzFieldEffortHH      = "Custom.EsfuerzoEstimadoHH"
AzFieldEffortRealHH  = "Custom.EsfuerzoRealHH"
AzFieldRemainingWork = "Microsoft.VSTS.Scheduling.RemainingWork"
```

## Conventions

- Methods are `private static` inside `partial class Program`. Don't change visibility without reason.
- All user-facing output goes through **Spectre.Console** (`AnsiConsole.MarkupLine`, `AnsiConsole.Write`, prompts). Reserve raw `Console.Write` for the encoding setup in `Main` and a few non-UI debug lines.
- Interactive menus use `SelectionPrompt<string>` and **must include the `BackOption`** so users can navigate without exiting.
- Errors: `AnsiConsole.MarkupLine("[red]❌ ...[/]")`. Silent `catch { }` is only for non-critical background work like `CheckForUpdates`.
- API calls use a 3-attempt retry with exponential backoff (1s, 2s, 4s) and only retry on 429 / 5xx.
- New model classes go in `Models.cs`, not inside `partial class Program`.
- Use `async/await` end-to-end — no `.Result` / `.Wait()`.

## Config & state

| Path | Purpose |
|------|---------|
| `~/.yitpush/config.json` | Active provider, API key, model (managed by `ConfigManager` in `AiProviders.cs`) |
| `~/.yitpush/version-check.json` | NuGet version-check cache (24h TTL) |

Environment variables (`OPENAI_API_KEY`, `ANTHROPIC_API_KEY`, `GOOGLE_API_KEY`, `DEEPSEEK_API_KEY`, `OPENROUTER_API_KEY`) override the stored API key for the matching provider. If no config file exists, the tool falls back to `DEEPSEEK_API_KEY` for backward compatibility — preserve this behavior.

## Dependencies

- `Spectre.Console` 0.54.0 — all interactive UI
- `TextCopy` 6.2.1 — clipboard for generated commit/PR text
- .NET 10 BCL only (HttpClient, JsonSerializer, Process, File). No additional packages without a strong reason.

External tools the CLI shells out to: `git` (always), `az` CLI (only for Azure DevOps commands), `npx` (only for `yp skill`).

## Related AI-agent docs in this repo

- `PROJECT_CONTEXT.md` — deep architectural notes (Claude Projects / custom agents)
- `.cursorrules` — Cursor / Windsurf rules
- `SKILL.md` and `skills/yp/SKILL.md` — Agent Skills package (Claude Code, Cursor, Gemini CLI)
- `llms.txt` / `llms-full.txt` — generic LLM-readable docs
- `README.md` — user-facing documentation
- `QUICKSTART.md` — 2-minute getting-started guide
- `CHANGELOG.md` — keep updated when shipping a new version
