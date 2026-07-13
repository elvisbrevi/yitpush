# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

---

## [Unreleased]

### Added
- **TUI for `yp setup`** — the interactive setup now launches a two-column live layout (`AnsiConsole.Live` + `Layout`) by default in a real terminal: left column lists the providers (with a green dot for the ones you've already tested), right column shows the API key status, endpoint, and the model picker, and a footer shows the result of the last save. Keyboard: `←/→` switch provider, `↑/↓` switch model, `T` test connection (re-uses `FetchModelsForProvider`), `Enter` save, `Esc` cancel. The "Test connection" re-uses the same logic as the wizard's validation step, so a tested provider gets a green dot in the list. The TUI auto-falls-back to the legacy 5-step wizard when stdin/stdout is redirected (CI), and the explicit flags `--wizard` and `--tui` let scripts force one or the other regardless of TTY detection. Resolves #15.
- **`yp setup --wizard`** — forces the legacy 5-step wizard. Auto-selected in CI, so existing scripts keep working.
- **`yp setup --tui`** — forces the TUI even if the routing would otherwise pick `--wizard` (useful for `script(1)` recordings or test harnesses that capture stdin but still want the visual TUI).
- **7 new xUnit tests** in `tests/YitPush.Tests/SetupTuiTests.cs` — cover `SetupTui.ShouldUseTui` across the auto-fallback decision matrix: no flags + interactive terminal, explicit `--tui`, explicit `--wizard`, stdin redirected (with and without `--tui`), stdout redirected, unknown args (ignored), and the `--tui --wizard` last-flag-wins tie-break.
- **`--no-spinner`** flag on `yp commit` and `yp pr` — skips the AnsiConsole.Status() spinner that wraps the AI call and the `git push`; the operation runs inline and the exit code is preserved. Same effect as the new `YITPUSH_NO_SPINNER` environment variable (`1`, `true`, or `yes` — case-insensitive). The spinner is also auto-disabled when stdout is redirected (CI logs), so a piped `yp commit` no longer leaves a frozen frame in the journal.
- **`YITPUSH_NO_SPINNER`** environment variable — global override for the spinner. Reads `1`, `true`, or `yes` (case-insensitive) and short-circuits the spinner wrapper everywhere it's used.
- **`Ui.RunWithStatus<T>(string title, Func<StatusContext?, Task<T>> work, bool noSpinner = false)`** — single helper for long operations. When the spinner is enabled it wraps the work in `AnsiConsole.Status().StartAsync`; when it's disabled (flag / env var / redirected stdout) it prints the title once and runs the work inline. Centralized so every long op picks up the same disable behavior.
- **12 new xUnit tests** in `tests/YitPush.Tests/UiTests.cs` — cover `Ui.ShouldDisableSpinner` (flag, env var case-insensitive, stdout redirect) and `Ui.RunWithStatus` (work invocation, exception propagation, return value, context ignore in the inline path).
- **2 new xUnit tests** in `tests/YitPush.Tests/CommitFormatTests.cs` — `ParseCommitArgs` recognizes `--no-spinner` and isolates it from the other commit flags.

### Changed
- **`CheckForUpdates` no longer renders a yellow Panel** that can interleave with the AnsiConsole.Status() spinner. The check now runs synchronously up-front and emits a single-line `⬆  yp <version> available` hint. The cache lookup is sub-100ms so the perceived startup latency is unchanged.
- **`yp --help`** adds a `--no-spinner` row under "Global flags" and the `commit` and `pr` option tables.
- **`yp --help` `setup` row** now mentions the TUI + `--wizard` escape hatch, and the example section adds `yp setup`, `yp setup --wizard`, and `yp setup --tui` lines so the keyboard shortcuts (`←/→`, `↑/↓`, `T`, `Enter`, `Esc`) are discoverable from `--help` alone.

### Fixed
- **Spectre.Console markup crash in `yp --help` on the `diff` table** (pre-existing, surfaced by the new diff subcommand shipped in v9): the JSON example contained literal `[...]` markup tags that crashed the markup tokenizer. Now rendered as a plain-text summary.

Resolves #14.

---

## [2.3.0] - 2026-07-10

### Changed
- **`yp azure-devops hu task` default is now 1 task** (was 3). The legacy trio (Desarrollo / Pruebas Unitarias / Code Review) is preserved as an opt-in via the new `--trio` flag or by passing `--task-titles "Desarrollo,Pruebas Unitarias,Code Review"`. Title precedence in single-task mode: `--title` > `taskTitles[0]` > `"Desarrollo"`. Projects that were relying on the implicit trio need to add `--trio` to their commands or scripts. Resolves #6.
- **`AzureDevOpsFlagParser.Parse(args)` is now the single source of truth** for all Azure DevOps flag parsing. The duplicated manual parser that lived at the top of `AzureDevOpsCommand.cs` is gone. `--description`/`-d`, `--no-link`/`-n`, `--effort`/`-e`, `--effort-real`/`-er`, `--remaining`/`-r`, `--state`/`-s`, `--comment`/`-c`, `--task-titles`/`-t`, `--repo`, `--branch` are all parsed by the same function used by `task update` / `hu show` / `hu list` / `task show`. This eliminates the "Quick mode ignores --task-titles" class of bugs.

### Added
- **`--title <text>`** flag on `yp azure-devops hu task` — patches the single task with the provided title. Highest precedence; overrides `--task-titles` and `--trio`.
- **`--trio`** flag on `yp azure-devops hu task` — restores the legacy three-task default (Desarrollo / Pruebas Unitarias / Code Review). Mutually exclusive with `--task-titles` (combined use exits 5).
- **22 new xUnit tests** — `HuTaskPlanTests` (8, covering the new plan computation: single, trio, custom, conflict, whitespace stripping, override precedence) and 14 new cases in `AzureDevOpsFlagParserTests` (all the new flags: --title, -d, -D, -e, -er, -r, -s, -c, -n, --no-link, --repo, --branch, -t, --trio). Total test count: 64 (was 42).
- **`yp azure-devops resolve-field <org> <project> <workItemType> <displayName>`** — prints the refname for a work item field by its display name (e.g. `Custom.EsfuerzoReal` for "Esfuerzo Real" in "Soluciones Transversales"). Exits `3` with a clear error when the field is unknown. Uses the disk-persisted cache so the second call is instant.
- **`yp azure-devops refresh-fields <org> <project> <workItemType>`** — invalidates the cache for the given `(org, project, workItemType)` triple and re-warms it with a fresh REST call.
- **`AzDevOpsFieldRefNameResolver.ResolveByDisplayNameAsync`** — generic, project-aware lookup that fetches `GET /_apis/wit/workitemtypes/{type}/fields?api-version=7.0` once and resolves any number of display names from the cached map.
- **`AzDevOpsFieldRefNameResolver.RefreshAsync`** — clears the in-memory + on-disk cache for a triple and re-warms it on demand.
- **Persistent field cache** at `~/.yitpush/field-cache.json` (24h TTL, same pattern as `models-cache.json`). Cached entries are isolated per `(org, project, workItemType)`. API errors (e.g. `TF51535`) invalidate the entry instead of caching an empty result, so transient issues self-heal on the next call.
- **`yp --version` / `yp -V`** — prints the assembly version (`2.3.0`) on stdout with no ANSI escapes, exits `0`. Intended for scripts and CI.
- **`--json` flag on `hu show`, `hu list`, `task show`** — emits a stable, flat JSON object on stdout (no Spectre.Console ANSI codes) so agents and scripts can pipe to `jq`:
  ```bash
  yp azure-devops hu show MyOrg 12345 --json | jq '.title'
  yp azure-devops hu list MyOrg MyProj 12345 --json | jq '.value | length'
  ```
  `hu show` / `task show` return `{id, type, title, state, assignedTo, createdDate, areaPath, iterationPath, effort, effortReal, remaining, month, urlCommit, description, relations?}`. Unknown custom fields pass through with their full refname. `hu list` returns `{huId, value: [{id, title, state}, ...]}`.
- **Non-interactive safety** for `hu show` / `hu list` — when stdout is piped (`| jq`, redirected to a file, etc.) the trailing `AnsiConsole.Prompt(...)` is skipped automatically, so non-interactive invocations never crash with the Spectre "isn't interactive" error.
- **`yp azure-devops task delete <org> <id> [--yes|-y] [--json]`** — moves a work item to the recycle bin via `DELETE /_apis/wit/recyclebin/{id}?api-version=7.0`. Prompts "Type 'yes' to confirm:" by default; in non-interactive mode (CI) the prompt is bypassed and the command exits `3` with a "Pass --yes to confirm" message unless `--yes` is passed. Exit codes: `0` on success, `1` if the work item is already gone (404), `2` on any other error. `12 new xUnit tests` in `tests/YitPush.Tests/AzDevOpsDeleteClientTests.cs`.
- **`yp azure-devops task attach <org> <project> <id> <file-path> [--comment "..."] [--json]`** — uploads a local file as an `AttachedFile` relation on the work item. Internally calls `POST /{project}/_apis/wit/attachments?fileName=<basename>&api-version=7.1-preview` (content-type `application/octet-stream`) and then `PATCH /_apis/wit/workitems/{id}` with a JSON-Patch `add` to `/relations/-`. The optional `--comment` becomes the tooltip on the attachment in the Azure Boards form. Validates the file exists before hitting the network. `8 new xUnit tests` in `tests/YitPush.Tests/AzDevOpsAttachmentClientTests.cs`.
- **`--yes` / `-y` flag** — added to `AzureDevOpsFlagParser`; used by `task delete` to skip the confirmation prompt in non-interactive contexts. `2 new unit tests` in `AzureDevOpsFlagParserTests`.
- **11 new unit tests** in `tests/YitPush.Tests/AzDevOpsFieldRefNameResolverTests.cs` (8 covering the new persistence / TTL / refresh / isolation / error behaviors, plus 3 updated existing tests). No live API calls in CI.
- **16 new unit tests** for the stable-output work — `InformationalVersionTests` (3), `FlattenWorkItemTests` (6), `BuildTaskListJsonTests` (5), plus 2 new cases in `AzureDevOpsFlagParserTests` for `--json`. All public-interface driven; no live API calls.
- **`--assigned-to <upn|display-name|"">`** flag on `yp azure-devops task update` — reassigns the work item to a new identity. Accepts a UPN (e.g. `elvis.brevi@sag.gob.cl`), a display name (e.g. `Elvis Brevi`), or an empty string to clear the assignment. The input is resolved against the project's identity store via `GET /_apis/identities?searchFilter=General&filterValue=<value>&api-version=7.0` (with a fallback to `GET /_apis/graph/users?api-version=7.1-preview.1` when the identity store returns nothing), and the resolved identity is written as the proper `{displayName, uniqueName, id}` object — not a raw UPN string. If the input matches multiple identities, the tool exits `4` and lists the candidates so the user can pick a more specific value. Per-session in-memory cache avoids repeat API calls when a batch update touches multiple tasks. Combines with `--state`, `--effort`, `--evidence`, etc. in a single PATCH (no round-trips). Resolves #7.
- **`AzDevOpsIdentityResolver`** — new internal helper (`AzureDevOps/AzDevOpsIdentityResolver.cs`) that turns a free-form input into a typed `IdentityResolutionResult` (SingleMatch / MultipleMatches / NotFound / Clear). Mirrors the design of `AzDevOpsFieldRefNameResolver`: HTTP via injected `HttpClient`, in-memory cache keyed by `(org, normalized input)`, never throws on the call path.
- **`TaskUpdateOperationsBuilder` now returns structured `Operation` records** — `StringFieldOperation` (string value), `IdentityFieldOperation` (identity object for `System.AssignedTo`), `ClearFieldOperation` (set value to `null` to clear a field). The legacy `BuildUpdateOperations` (string list) is preserved as a thin wrapper for callers that only need string fields. `UpdateWorkItemViaRestApi` was refactored to emit the right JSON-Patch `value` shape for each operation kind.
- **15 new xUnit tests** — `AzureDevOpsIdentityResolverTests` (8 covering single/multiple/not-found/empty/whitespace/cache-hit/cache-case-insensitive/per-org-isolation), `TaskUpdateOperationsBuilderTests` (4 for the new `BuildUpdateOperationsStructured` covering clear / identity / absent / mix-with-other-fields), and `AzureDevOpsFlagParserTests` (3 for the new `--assigned-to` flag including the empty-string case). Total test count: 106 (was 91).
- **`yp azure-devops link <org> <proj> <id> --repo <r> --branch <b>`** now honors quick mode for both HUs and Tasks (previously only `hu link` did). With both flags the interactive menu is skipped and the `ArtifactLink` is created directly. When the target work item is a Task, the `Custom.URLCommit` field is also written as a navigation fallback for legacy `az` scripts (no-op if the field doesn't exist). Omit the flags to keep the previous menu flow. Resolves #9.
- **22 new xUnit tests** — `AddLinkToRepoHelpersTests` covers the pure URL-construction, menu-skip predicate, repo lookup, and JSON-Patch body builders (`BuildBranchLinkArtifact`, `IsQuickModeLink`, `FindRepoByName`, `BuildArtifactLinkPatchBody`) plus 4 routing tests for the `link` subcommand (quick-mode with both flags, fallback with none, partial coverage with only one). Total test count: 128 (was 106).
- **NVIDIA NIM provider** (`NVIDIA_API_KEY` env var) — adds a sixth AI provider to `yp` with a free tier. `yp setup` now lists "NVIDIA NIM" alongside OpenAI / Anthropic / Google Gemini / DeepSeek / OpenRouter. The NIM endpoint (`https://integrate.api.nvidia.com/v1`) is OpenAI-compatible on the wire, so the existing `CallOpenAiCompatibleApi` path serves it directly. Live model discovery is via `GET /v1/models` with `Authorization: Bearer $NVIDIA_API_KEY`; the result is cached for 24h in `~/.yitpush/models-cache.json` exactly like the other providers. Fallback list when the live fetch fails: `meta/llama-3.1-70b-instruct`, `meta/llama-3.1-8b-instruct`, `nvidia/nemotron-4-340b-instruct`. On-prem NIM deployments can override the base URL via `pc.BaseUrl` in the config (the chat-completions suffix is stripped to reach `/v1/models`). `NVIDIA_API_KEY` overrides the stored key at runtime, matching the existing `OPENAI_API_KEY` / `ANTHROPIC_API_KEY` / `GOOGLE_API_KEY` / `DEEPSEEK_API_KEY` / `OPENROUTER_API_KEY` convention. Resolves #16.
- **7 new xUnit tests** — `ProviderDispatchTests` covers the NIM routing decision (default base URL for `Nvidia` and `NVIDIA NIM`, fallback model list with the three well-known NIM models, `FetchModelsForProvider` with a hand-rolled `HttpMessageHandler` stub for the `/v1/models` call, Bearer-token header propagation, empty-list on non-2xx, custom base URL for on-prem NIM) and the env-var override path (`NVIDIA_API_KEY` overrides the stored key and routes to the NIM base URL via `GetAiInfo`). Total test count: 135 (was 128).
- **Pre-flight check for `Done` transitions on `yp azure-devops task update`** — when `--state "Done"` is passed without `--evidence`, the tool first `GET`s the work item and rejects the transition (exit `2`, no PATCH) if the project-specific `Evidencias de finalización` field is empty. The check is skipped for non-`Done` states, when `--evidence` is provided (the flag itself populates the field), or when the work item already has the field populated. The pre-flight uses `AzDevOpsFieldRefNameResolver` to discover the project's refname (no hardcoded GUIDs), runs the GET in a separate `HttpClient` so the existing PATCH `HttpClient` lifetime is unchanged, and is fail-open on any auth/network/5xx/unresolved-refname failure so the existing PATCH flow is not regressed. New `TaskUpdatePreFlight` helper exposes `BuildMissingEvidenceMessage(displayName, refName)` (pure) and `RunAsync(PreFlightRequest)` (returns `PreFlightPassed` / `PreFlightSkipped(reason)` / `PreFlightFailed(message)`). Resolves #17.
- **5 new xUnit tests** in `tests/YitPush.Tests/TaskUpdatePreFlightTests.cs` (plus 3 existing `BuildMissingEvidenceMessage` tests) — cover the four pre-flight branches: state=Done + `--evidence` provided → `PreFlightPassed` with no HTTP calls; state≠Done → `PreFlightPassed` with no HTTP calls; state=Done + no `--evidence` + work item has evidence → `PreFlightPassed` after one GET; state=Done + no `--evidence` + work item has empty evidence → `PreFlightFailed` carrying the display name, refname, and `--evidence` hint; state=Done + GET 5xx → `PreFlightSkipped` (fail-open). Total test count: 140 (was 135).
- **`yp commit` — Conventional Commits + custom layout (`--conventional`, `--type`, `--scope`, `--detect-breaking`, `--amend`, `--template`)** — emitted messages now follow the `<type>(<scope>)?!?: <subject>` convention with an optional `BREAKING CHANGE: <reason>` footer. `--type` and `--scope` force the AI's choice; `--detect-breaking` runs a static-analysis pass over the diff (regex for `^-.*\bpublic ...` in C#, `^-.*\bexport ...` in TS/JS, `^+.*"version": "<MAJOR>.0.0"` in JSON, `^+.*#major.bump` markers) and feeds the markers into the prompt and, when `--conventional` is set, into the rendered footer. `--amend` re-invokes the AI on `git diff HEAD~1` (not the working tree) and rewrites the last commit in place via `git commit --amend -F <tmpfile>` — no `git add`, no push. `--template <path-to-md>` renders the AI output through a Handlebars-ish template (`{{type}}`, `{{scope}}`, `{{subject}}`, `{{body}}`, `{{refs}}`); missing variables resolve to an empty string, malformed tokens are left untouched. The tool exits `6` when the template file is missing. The default format for every project can be set under a new `"commitFormat"` key in `~/.yitpush/config.json` (`"conventional"`, `"plain"`, `"gitmoji"`, or any path to a template file); explicit `--conventional` / `--template` flags override the config for that one call. Resolves #11.
- **48 new xUnit tests** in `tests/YitPush.Tests/CommitFormatTests.cs` — cover template rendering with all 5 vars / missing-vars fallback / malformed-token passthrough, conventional commit parsing (plain subject, with scope, bang breaking marker, BREAKING CHANGE footer extraction, whitespace tolerance, all 10 documented types, rejection of non-conventional and unknown-type subjects), conventional commit formatting (plain, scoped, breaking reason, body+footer separation), breaking-change detection (clean diff, removed C# public symbol, removed TS/JS export, JSON major bump, `#major.bump` marker, no false positive on minor bump), `CommitArgs` parsing (defaults, legacy flags, all six new flags in dedicated and `=` forms, `commitFormat` config fallback, override precedence), final message assembly (passthrough, conventional normalization, bang + footer append, AI-footer preserved, type/scope override), template rendering with disk-backed `{{vars}}` (success, missing-var fallback, `FileNotFoundException` when the template path is missing), and AI prompt construction (type/scope constraint, BREAKING CHANGE footer mention, no constraints when no format is set, breaking-marker augmentation, omission when `--detect-breaking` is off). Total test count: 188 (was 140).

### Notes
- This unlocks fixing the long-standing BUG-001 (`EsfuerzoRealHH` vs `EsfuerzoReal`) and the `Remaining Work` verification todo in `KNOWN_BUGS.md` without touching the hardcoded constants. The new CLI subcommands let users look up the correct refname on demand while the constants are gradually migrated.

---

## [2.2.2] - 2026-06-22

### Added
- **`--title`** flag on `task update` — patches `System.Title`.
- **`--description` / `-D`** flag on `task update` — patches `System.Description`.
- **`--evidence`** flag on `task update` — resolves the project-specific "Evidencias de finalización" field by display name (cached per project/work-item-type) and writes to it. No more hardcoded GUIDs.
- **`--field <RefName=value>`** generic flag on `task update` — repeatable escape hatch for any work-item field.
- **`--history`** flag on `task update` — explicit opt-in to write to the legacy `System.History` field (the old behavior of `--comment`).
- **xUnit test project** (`tests/YitPush.Tests/`) with 18 unit tests covering the new flag parser, operations builder, field resolver, and comment client. First automated tests in the repo.

### Changed
- **`--comment` no longer patches `System.History`** — it now creates a discussion comment via `POST /_apis/wit/workItems/{id}/comments`. Use the new `--history` flag if you need the old behavior. This is a breaking change; v2.2.1 users relying on `--comment` populating `System.History` must update their scripts to `--history`.

---

## [2.2.1] - 2026-06-22

## [2.2.1] - 2026-06-22

### Fixed
- **`--effort` / `-e`, `--effort-real` / `-er`, `--task-titles` / `-t` silently ignored** — flag parser in `AzureDevOpsCommand` was missing these three branches, so values passed to `yp azure-devops task update` and `hu task` were dropped before reaching the API. Also removed a duplicate `--comment` / `-c` parser block.

---

## [2.2.0] - 2026-05-18

### Changed
- **Task update via REST API** — `UpdateWorkItem` now uses Azure DevOps REST API (`PATCH /wit/workitems/{id}`) instead of Azure CLI. This unifies task creation and update under the same HTTP-based approach.
- **Comments via REST API** — Comments are now sent as a JSON Patch operation to `System.History` in the same request as field updates, instead of a separate `az boards work-item update --discussion` call.

### Documentation
- **Agent skill updated** — `.agents/skills/yp/SKILL.md` decision table now includes `-t`, `-n` flags for `hu task`, effort-real for `task update`, and the `wi update` / `hu update` aliases.
- **README updated** — Anthropic model names include version date suffix; `hu task` example shows `-t` and `-n` flags.
- **CLI help updated** — `ShowHelp()` short flags note includes `-t` and `-n`.

---

## [2.1.8] - 2026-05-13

### Fixed
- **Task description format** — Descriptions now preserved as raw Markdown via Azure DevOps REST API. Newlines, headers, lists, and formatting render correctly since the API stores JSON with proper `\n` encoding. No conversion needed — Azure DevOps renders Markdown natively.

---

## [2.1.7] - 2026-05-13

### Added
- **`--no-link` / `-n` flag** for `hu task` — skips the branch-linking prompt entirely, enabling fully non-interactive task creation.
- **`--repo` + `--branch` auto-link** for `hu task` — when both flags are provided, tasks are linked to the specified branch automatically without prompting.

### Fixed
- **Task description formatting** — Markdown descriptions are now converted to HTML and stored in `Microsoft.VSTS.Common.DescriptionHtml` via Azure DevOps REST API. Headers, lists, bold, italic, and code formatting render correctly in Azure DevOps.

---

## [2.1.6] - 2026-05-13

### Fixed
- **Documentation** — Documented `--task-titles` / `-t` flag in CLI help and agent skill file. It was supported but undocumented, making it impossible to discover.

---

## [2.1.5] - 2026-05-13

### Fixed
- **Non-interactive task creation** — Added `--task-titles` / `-t` flag to `yp azure-devops hu task` for scripted/non-interactive usage (e.g. piping markdown descriptions). Previously the command would fail with `Failed to read input in non-interactive mode` when description was passed via `--description`.

---

## [2.1.4] - 2026-04-29

### Added
- **Interactive Search in Menus** — Enabled instant search/filtering in all significant selection lists. You can now type to quickly find items in:
  - Azure DevOps: Organizations, Projects, Repositories, User Stories, Tasks, and Variable Groups.
  - Git: Branch selection for `checkout` and `pr` commands.
  - Setup: AI Provider and Model selection (including long lists from OpenRouter).
  - Main menus: Azure DevOps interactive menu.

---

## [2.1.3] - 2026-04-28

### Fixed
- **`[Custom...]` option causes crash in `yp setup`** — the `[Custom...]` model selection entry was not escaped for Spectre.Console markup, causing `Encountered malformed markup tag` exception. Now escaped with `Markup.Escape` before being added to the list.

---

## [2.1.2] - 2026-04-27

### Fixed
- **`yp setup` crash on model selection** — model names containing `[`, `]`, `<`, `>`, `&` (returned by provider APIs) are now escaped before rendering in the Spectre.Console `SelectionPrompt`, preventing `Encountered malformed markup tag` exceptions.

---

## [2.1.0] - 2026-04-27

### Added
- **Live model discovery in `yp setup`** — the model selection menu is now populated by querying each provider's `/models` endpoint, so newly released models are picked up without updating the app.
  - OpenAI / DeepSeek: `GET /v1/models` with `Authorization: Bearer` (filtered to chat-capable model families: `gpt-*`, `o1`, `o3`, `o4`, `chatgpt-*`).
  - OpenRouter: `GET /api/v1/models` (no auth required); honors the host of any custom base URL configured in setup.
  - Anthropic: `GET /v1/models?limit=100` with `x-api-key` and `anthropic-version`.
  - Google Gemini: `GET /v1beta/models?key=…`, filtered to models that support `generateContent`.
- **Models cache** — fetched lists are stored in `~/.yitpush/models-cache.json` with a 24h TTL, so the menu opens instantly between runs.
- **Live/defaults indicator** — the model selection title now shows `(live)` or `(defaults)` so it's clear whether the list came from the provider or the built-in fallback.
- **`CLAUDE.md`** — guidance file for Claude Code with build/run/pack commands, architecture notes, conventions, and pointers to the other AI-agent docs in the repo.

### Changed
- The model selection menu in `yp setup` now paginates at 15 entries to handle providers like OpenRouter that return 100+ models.
- `[Custom…]` remains available in every provider's menu as an escape hatch for typing any model ID by hand.

### Notes
- If the live fetch fails (network issue, invalid key, schema change), `yp setup` falls back silently to the curated `GetDefaultModelsForProvider` list — existing behavior is preserved.

---

## [2.0.0] - 2026-03-21

### Added
- **Multi-provider AI support** — choose between OpenAI, Anthropic, Google Gemini, DeepSeek, and OpenRouter.
- **`yp setup` command** — interactive wizard to configure your AI provider, API key, and model. Saves to `~/.yitpush/config.json`.
- **OpenRouter provider** — access 100+ models through a single API key with optional custom base URL.
- **Anthropic provider** — native support for Claude models using the Messages API.
- **Google Gemini provider** — native support using the generateContent API.
- **`-l` short flag** for `--language` in both `commit` and `pr` commands.
- **Automatic alias setup** — `yp setup` now offers to add `alias yitpush='yp'` to the user's shell config (`.zshrc`, `.bashrc`, or `.profile`). On Windows, it shows the PowerShell equivalent.
- **Update notifications** — on every run, `yp` checks NuGet for a newer version (at most once per day, cached in `~/.yitpush/version-check.json`). If a newer version is found, a message is shown with the update command: `dotnet tool update -g YitPush`.
- **AI-friendly assets** — added `llms.txt`, `llms-full.txt`, `.cursorrules`, `PROJECT_CONTEXT.md`, and `SKILL.md` for AI agent discoverability.
- **Agent Skills package** — skill published at `skills/yp/SKILL.md`, compatible with the [Agent Skills open standard](https://agentskills.io). Install with `npx skills add elvisbrevi/yitpush`.
- **Provider-aware progress messages** — commit and PR commands now show the active provider name and model.
- **Backward compatibility** — `DEEPSEEK_API_KEY` environment variable still works without any config file.
- Environment variable overrides (`OPENAI_API_KEY`, `ANTHROPIC_API_KEY`, `GOOGLE_API_KEY`, `OPENROUTER_API_KEY`) take precedence over stored config.

### Changed
- `CommitCommand` and `GeneratePrDescription` no longer hardcoded to DeepSeek — they use the active configured provider.
- Version bumped to 2.0.0 to reflect the breaking change in configuration model.

---

## [1.4.0] - 2026-03-14

### Added
- Command shortened to **`yp`** for faster usage (`yitpush` alias still supported).
- **Esfuerzo Real HH** field support in `task update` command (`--effort-real` / `-er`).
- Full Azure DevOps Task Management: `hu list`, `task show`, `task update`.
- Interactive and direct updates for Effort, Esfuerzo Real HH, Remaining Work, State and Comments.
- Smart state validation and selection menus for Azure DevOps.
- Integrated "List all fields" tool for debugging work item technical names.

### Improved
- Faster navigation with `← Back` support in all interactive menus.
- Sorting by ID descending (recency first) in all Azure DevOps lists.

---

## [1.3.0] - 2026-02-01

### Added
- `yp azure-devops link` — add branch/commit/PR links to any work item.
- `yp azure-devops hu link` — link a repository branch to a User Story using ArtifactLink (shows in Azure Boards Development section).

---

## [1.2.0] - 2026-01-15

### Added
- `yp azure-devops hu show` — show User Story details (title, effort, description, links).
- `yp azure-devops task show` — show Task details.
- `yp azure-devops hu task` — create tasks for a User Story interactively or via CLI args (quick mode).

---

## [1.1.0] - 2025-12-01

### Added
- `yp pr` — generate pull request descriptions between two branches using AI.
- `--detailed` flag for both `commit` and `pr` commands.
- `--language` / `--lang` flag for `commit` and `pr` commands.
- `--save` flag to write output to a markdown file.
- Interactive branch selection with pagination.

---

## [1.0.0] - 2025-11-01

### Added
- Initial release.
- `yp commit` — AI-generated commit messages using DeepSeek.
- `yp checkout` — interactive branch checkout.
- `yp azure-devops repo new` — create Azure DevOps repositories.
- `yp azure-devops repo checkout` — clone repositories interactively.
- `yp azure-devops variable-group list` — list and inspect variable groups.
- `--confirm` flag for commit review before pushing.
