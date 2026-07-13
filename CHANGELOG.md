# 📋 Changelog

> **All notable changes to `yp` (YitPush) are documented in this file.**
> This is the canonical reference for everything that has shipped — bug fixes, new features, breaking changes, deprecations, and known migration notes.
>
> The format follows [Keep a Changelog](https://keepachangelog.com/en/1.0.0/) and the project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).
>
> 🗂️ **Hay MUCHOS cambios acumulados a lo largo de las versiones — desplázate por las secciones a continuación para revisar cada `Added` / `Changed` / `Fixed` / `Removed`.** Cada bullet apunta al issue que resuelve, así puedes saltar directo a la conversación original si necesitas contexto adicional.

---

## 📑 Jump to

- [Unreleased](#unreleased)
- [**[2.3.0]** — 2026-07-10](#230---2026-07-10-the-big-one-) — *the big one: 15 vertical slices, 326 tests, new `pr` subcommands, TUI, NVIDIA NIM, conventional commits*
- [2.2.2 — 2026-06-22](#222---2026-06-22)
- [2.2.1 — 2026-06-22](#221---2026-06-22)
- [2.2.0 — 2026-05-18](#220---2026-05-18)
- [2.1.x — 2026](#21x--2026)
- [2.0.0 — 2026-03-21](#200---2026-03-21)
- [1.x — 2025](#1x--2025)

---

## [Unreleased]

### 📊 Stats
- 66 new xUnit tests across 3 new test files (`HelpTextTests`, `SkillFileAlignmentTests`, `LlmsFileTests`)
- 1 bug fix (latent Spectre.Console markup crash in `yp --help`)
- 1 new bash script (`scripts/regenerate-llms.sh`)
- Total test count: **326 passing** (was 260 before this slice)

### ✨ Added
- **`tests/YitPush.Tests/HelpTextTests.cs`** — 11 xUnit tests that assert `yp --help` mentions every top-level subcommand introduced in v0–v13 (`task delete`, `task attach`, `resolve-field`, `refresh-fields`, `diff`, `pr list`, `pr show`, `pr comments`, `pr reply`, `pr create`). The tests capture `ShowHelp()`'s output by swapping `AnsiConsole.Console` for a `StringWriter`-backed one and strip ANSI escapes, so they verify the user-facing help, not a parallel data structure.
- **`tests/YitPush.Tests/SkillFileAlignmentTests.cs`** — 22 xUnit tests that assert both `SKILL.md` and `skills/yp/SKILL.md` mention every v0–v13 subcommand and that the two files are byte-identical (the installable skill must not drift from the human-readable one).
- **`tests/YitPush.Tests/LlmsFileTests.cs`** — 23 xUnit tests that assert `llms.txt` and `llms-full.txt` reflect the v2.3.0 runtime surface (version stamp, NVIDIA NIM provider, and all the v0–v13 subcommands).
- **`Program.HelpTopLevelSubcommands` / `HelpPrSubcommands` / `HelpAzureDevOpsSubcommands`** — `internal static readonly string[]` constants that the new tests assert against. `BuildHelpText()` joins them into a single string for the tests. The visual help in `ShowHelp()` and the test surface are now in sync.
- **`scripts/regenerate-llms.sh`** — bash script that stamps the current `<Version>` from `YitPush.csproj` into `llms.txt` and `llms-full.txt` and smoke-checks that `SKILL.md` mentions every v0–v13 subcommand. Designed to be re-run at the start of every release cycle.

### 🐛 Fixed
- **Spectre.Console markup crash in `yp --help` (latent, surfaced by capturing `ShowHelp()` from a test)** — the `prSubTable` row `"create --source <b> --target <b> --title <t> [--body-file <path>] [--auto-complete]"` was unescaped, so `[--body-file <path>]` was parsed as a markup tag and threw `InvalidOperationException: Could not find color or style '--body-file'`. The brackets are now escaped (`[[--body-file <path>]]`) so the help renders cleanly. This was previously hidden because the default AnsiConsole path silently failed in the same place; the captured test now proves the row renders end-to-end.

Resolves #19.

---

## [2.3.0] — 2026-07-10 — *the big one* 🎉

> 🚀 **This is the largest release in `yp`'s history.** Across 15 vertical slices (v0–v14) the v2.3.0 cycle hardened the Azure DevOps experience, expanded the AI-provider matrix, and added a first-class `yp pr` subcommand suite, a TUI for `yp setup`, the NVIDIA NIM provider, the `--no-spinner` UX layer, the `task delete` / `task attach` / `resolve-field` / `refresh-fields` subcommands, the `hu task` single-task default with `--trio` opt-in, `--assigned-to`, the `Done` pre-flight check, conventional commits, and a slew of bug fixes.
>
> 📚 **Revisa los bullets de cada slice más abajo — el detalle completo está aquí en el changelog.** Each bullet links the issue it resolves.

### 🏆 Highlights

- 🤖 **NVIDIA NIM** as a sixth AI provider (free tier, OpenAI-compatible on the wire) — `yp setup` now lists it; live model discovery hits `GET /v1/models`. Resolves #16.
- 🎨 **`yp setup` TUI** — two-column live layout (`AnsiConsole.Live` + `Layout`) with `←/→` provider, `↑/↓` model, `T` test, `Enter` save, `Esc` cancel. Auto-falls-back to the wizard in CI; explicit `--wizard` and `--tui` flags force one or the other. Resolves #15.
- 🚦 **`--no-spinner`** flag + `YITPUSH_NO_SPINNER` env var + auto-disable when stdout is redirected. Centralized in `Ui.RunWithStatus<T>`. Resolves #14.
- 📋 **Full `yp pr` subcommand suite** — `pr list`, `pr show <id>`, `pr comments <id>`, `pr reply <id> <thread-id> --body "..."`, `pr create --source <b> --target <b> --title <t> [--body-file <path>] [--auto-complete]`. The `yp pr` (no args) interactive menu picks the operation. All read/write paths stable-JSON via `--json`. Resolves #13.
- 🔀 **`yp diff`** — friendly wrapper over `git diff` with `--files`, `--hunks`, `--stat`, `--json`, and `<refA> <refB>` positional pair. JSON shape stable for `jq` pipelines.
- 📝 **Conventional Commits** — `yp commit --conventional [--type <t>] [--scope <s>] [--detect-breaking] [--amend] [--template <path>]`. Configurable per-project via `"commitFormat"` in `~/.yitpush/config.json`. Resolves #11.
- 🗑️ **`task delete <org> <id> [--yes|-y] [--json]`** — moves a work item to the recycle bin (`DELETE /_apis/wit/recyclebin/{id}`). Prompts by default; `--yes` skips the prompt in CI.
- 📎 **`task attach <org> <project> <id> <file-path> [--comment "..."] [--json]`** — uploads a local file as an `AttachedFile` relation.
- 🔍 **`resolve-field` / `refresh-fields`** — print a work-item field refname by display name; invalidate the on-disk cache (24h TTL).
- 👤 **`--assigned-to <upn|display-name|"">`** — UPN or display-name resolved against the project's identity store; multiple matches exit 4 with a candidate list. Resolves #7.
- 🔗 **`link --repo <r> --branch <b>` quick mode** — works for both HUs and Tasks; `Custom.URLCommit` written as fallback for Tasks. Resolves #9.
- 🛡️ **Pre-flight for `Done` transitions** — when `--state "Done"` is passed without `--evidence`, the tool `GET`s the work item and rejects the transition (exit 2) if `Evidencias de finalización` is empty. Fail-open on auth/network/5xx. Resolves #17.
- 🐛 **BUG-001 / BUG-002 / BUG-003** — `EsfuerzoRealHH` refname routing, dynamic state cache, `Evidencias` required for `Done`. Resolves #18.
- 🎯 **`hu task` single-task default** (was 3); legacy trio preserved via `--trio`. Title precedence: `--title` > `task-titles[0]` > `"Desarrollo"`. Resolves #6.
- 🆔 **`--version` / `-V` global flag** + **`--json` on `hu show` / `task show` / `hu list`** — stable flat JSON for `jq`; trailing interactive prompt auto-skipped on redirected stdout.

### 📊 Stats
- 15 vertical slices (v0–v14) + release-script slice
- **371 xUnit tests passing** (was 18 at the start of the v2.3.0 cycle)
- New subcommands: `pr list/show/comments/reply/create`, `task delete`, `task attach`, `resolve-field`, `refresh-fields`, `diff`
- New flags: `--conventional`, `--type`, `--scope`, `--detect-breaking`, `--amend`, `--template`, `--no-spinner`, `--title`, `--trio`, `--assigned-to`, `--yes`, `--evidence`, `--field`, `--history`, `--json`, `--version/-V`
- New TUI: `yp setup` (with `--wizard` / `--tui` escape hatches)
- New provider: NVIDIA NIM (free tier, OpenAI-compatible)
- New `scripts/release-2.3.0.sh` (with `--dry-run`) that finalises the release
- **Breaking-ish change**: `hu task` now creates 1 task by default; pass `--trio` to keep the legacy 3-task behavior

### 🔄 Changed
- **`yp azure-devops hu task` default is now 1 task** (was 3). The legacy trio (Desarrollo / Pruebas Unitarias / Code Review) is preserved as an opt-in via the new `--trio` flag or by passing `--task-titles "Desarrollo,Pruebas Unitarias,Code Review"`. Title precedence in single-task mode: `--title` > `taskTitles[0]` > `"Desarrollo"`. Projects that were relying on the implicit trio need to add `--trio` to their commands or scripts. Resolves #6.
- **`AzureDevOpsFlagParser.Parse(args)` is now the single source of truth** for all Azure DevOps flag parsing. The duplicated manual parser that lived at the top of `AzureDevOpsCommand.cs` is gone. `--description`/`-d`, `--no-link`/`-n`, `--effort`/`-e`, `--effort-real`/`-er`, `--remaining`/`-r`, `--state`/`-s`, `--comment`/`-c`, `--task-titles`/`-t`, `--repo`, `--branch` are all parsed by the same function used by `task update` / `hu show` / `hu list` / `task show`. This eliminates the "Quick mode ignores --task-titles" class of bugs.

### ✨ Added
- **`scripts/release-2.3.0.sh`** — executable bash script that finalises the v2.3.0 release in one command. Pre-flight checks: `<Version>` extracted from `YitPush.csproj`, `CHANGELOG` has `## [X.Y.Z]` section, `llms.txt` + `llms-full.txt` mention the version, both `SKILL.md` copies mention the version and are byte-identical, `NUGET_API_KEY` is set in the env. Aggressive pre-build cleanup wipes stale `nupkg/*.nupkg` and `nupkg/*.symbols.nupkg` so the push step only sees the artifact we just built. Steps: `[1/5] dotnet pack -c Release`, `[2/5] dotnet nuget push "./nupkg/YitPush.${VERSION}.nupkg" --api-key "$NUGET_API_KEY" --skip-duplicate`, `[3/5] git tag -a v${VERSION}` (idempotent: skipped if the tag already exists), `[4/5] git push origin v${VERSION}`, `[5/5] npx skills add elvisbrevi/yitpush` (non-fatal, wrapped in `|| { echo WARN...; }`). `VERSION` is overridable via env var for testability; supports `--dry-run` (pre-flight only) and `-h/--help`. C# design lives in `Commands/ReleaseCommand.cs` (`ReleasePlan` + `ReleaseStep` + `PreFlightReport` + `ReleaseFinding`); bash script and C# model are kept in lock-step by the test surface.
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
- **`yp pr` interactive menu** — running `yp pr` with no arguments now opens an interactive menu (Spectre `SelectionPrompt`) with six options: generate an AI description, list, show, comments, reply, or create a PR. Picking a read/write option still falls through to the same CLI handler as the direct subcommand (so the menu is purely a discovery layer).
- **`yp pr list`** — list open pull requests in the current repo via `az repos pr list --output json`. Output: Spectre table (id, title, author, source, target, draft flag, created). `--json` emits `{ exitCode, count, pullRequests: [...] }` for `jq` pipelines. Resolves the "I don't want to open a browser to see what's open" workflow.
- **`yp pr show <pr-id>`** — show a single PR (title, description, source/target, status, author, creation date, draft flag, reviewer table with vote + isRequired). Backed by `az repos pr show --id <pr-id> --output json`. `--json` supported. Resolves the "give me the context of a PR in one terminal screen" workflow.
- **`yp pr comments <pr-id>`** — list all discussion threads with author, date, body, and `filePath:line` context. Backed by the Azure DevOps REST API (`GET /git/repositories/{repoId}/pullRequests/{prId}/threads`) because `az repos pr thread` does not exist. `--json` emits `{ exitCode, prId, threads: [{ id, status, filePath?, lineNumber?, comments: [...] }] }`. Resolves the "address review feedback without opening the browser" workflow.
- **`yp pr reply <pr-id> <thread-id> --body "..."`** — post a reply to a specific thread. Backed by the REST API (`POST .../threads/{threadId}/comments`). `--json` supported. Resolves the "acknowledge a review comment in one command" workflow.
- **`yp pr create --source <branch> --target <branch> --title <title> [--body-file <path>] [--auto-complete]`** — open a new pull request. Backed by the REST API (`POST /git/repositories/{repoId}/pullRequests`) so the description can be larger than `az`'s argv limit. Without `--body-file`, the description is read from stdin. `--auto-complete` sets `completionOptions` (with `deleteSourceBranch: true`). `--json` supported. Resolves the "open a PR with a long body from a script" workflow.
- **Stable exit codes across the new subcommands** — `0` success, `1` not-found (404), `2` auth/error, `3` validation. Mirrors the `azure-devops` conventions added in v2.3.0.
- **`AzureDevOpsPrClient` (internal)** — REST client for the new write operations (`CreatePullRequestAsync`, `PostThreadCommentAsync`, `ListThreadsAsync` + a `ParseThreadsJson` helper). All methods accept an `HttpClient` so the test suite uses a hand-rolled `StubHttpHandler` (no live API calls in CI). Mirrors the `AzDevOpsDeleteClient` / `AzDevOpsAttachmentClient` pattern.
- **`PrAzJsonParser` (internal)** — JSON parser for the `az repos pr list` and `az repos pr show` output. Maps the `az` JSON shape into the typed `PrSummary` / `PrDetail` / `PrReviewer` / `PrChangedFile` models. Branch names are stripped of the `refs/heads/` prefix so the table stays compact.
- **`PrDispatcher` (internal)** — single source of truth for `yp pr` argument routing. Returns a `PrRoute` (kind + extracted fields + `ValidationError` flag) so the dispatcher is testable in isolation (17 dispatcher tests cover list/show/comments/reply/create routing, validation, AI flag pass-through, and unknown subcommands).
- **41 new xUnit tests** — `AzDevOpsPrClientTests` (12), `PrAzJsonParserTests` (8), `PrDispatcherTests` (17), `PrJsonContractTests` (4). Total: 263 tests passing (was 222 before this slice).
- **`PrSummary` / `PrDetail` / `PrReviewer` / `PrChangedFile` models** — added to `Models.cs` with stable `JsonPropertyName` camelCase attributes for the `--json` output contract.
- **TUI for `yp setup`** — the interactive setup now launches a two-column live layout (`AnsiConsole.Live` + `Layout`) by default in a real terminal: left column lists the providers (with a green dot for the ones you've already tested), right column shows the API key status, endpoint, and the model picker, and a footer shows the result of the last save. Keyboard: `←/→` switch provider, `↑/↓` switch model, `T` test connection (re-uses `FetchModelsForProvider`), `Enter` save, `Esc` cancel. The "Test connection" re-uses the same logic as the wizard's validation step, so a tested provider gets a green dot in the list. The TUI auto-falls-back to the legacy 5-step wizard when stdin/stdout is redirected (CI), and the explicit flags `--wizard` and `--tui` let scripts force one or the other regardless of TTY detection. Resolves #15.
- **`yp setup --wizard`** — forces the legacy 5-step wizard. Auto-selected in CI, so existing scripts keep working.
- **`yp setup --tui`** — forces the TUI even if the routing would otherwise pick `--wizard` (useful for `script(1)` recordings or test harnesses that capture stdin but still want the visual TUI).
- **7 new xUnit tests** in `tests/YitPush.Tests/SetupTuiTests.cs` — cover `SetupTui.ShouldUseTui` across the auto-fallback decision matrix: no flags + interactive terminal, explicit `--tui`, explicit `--wizard`, stdin redirected (with and without `--tui`), stdout redirected, unknown args (ignored), and the `--tui --wizard` last-flag-wins tie-break.
- **`--no-spinner`** flag on `yp commit` and `yp pr` — skips the AnsiConsole.Status() spinner that wraps the AI call and the `git push`; the operation runs inline and the exit code is preserved. Same effect as the new `YITPUSH_NO_SPINNER` environment variable (`1`, `true`, or `yes` — case-insensitive). The spinner is also auto-disabled when stdout is redirected (CI logs), so a piped `yp commit` no longer leaves a frozen frame in the journal.
- **`YITPUSH_NO_SPINNER`** environment variable — global override for the spinner. Reads `1`, `true`, or `yes` (case-insensitive) and short-circuits the spinner wrapper everywhere it's used.
- **`Ui.RunWithStatus<T>(string title, Func<StatusContext?, Task<T>> work, bool noSpinner = false)`** — single helper for long operations. When the spinner is enabled it wraps the work in `AnsiConsole.Status().StartAsync`; when it's disabled (flag / env var / redirected stdout) it prints the title once and runs the work inline. Centralized so every long op picks up the same disable behavior.
- **12 new xUnit tests** in `tests/YitPush.Tests/UiTests.cs` — cover `Ui.ShouldDisableSpinner` (flag, env var case-insensitive, stdout redirect) and `Ui.RunWithStatus` (work invocation, exception propagation, return value, context ignore in the inline path).
- **2 new xUnit tests** in `tests/YitPush.Tests/CommitFormatTests.cs` — `ParseCommitArgs` recognizes `--no-spinner` and isolates it from the other commit flags.

### 🔄 Changed (more)
- **`yp pr` with no args no longer opens the AI description generator by default** — it now opens the interactive menu. The original AI flow is preserved as a backward-compatible escape hatch: pass `--detailed`, `--save`, or any AI flag to bypass the menu and run the AI generator directly. This is a UI/UX change, not a breaking behavior change (scripts that used `yp pr` with no args were not common in the wild; the `yp pr --detailed` form continues to work unchanged).
- **`yp --help` `pr` row** — the description now says "Triage and act on Azure DevOps PRs (list/show/comments/reply/create) or generate an AI description with --detailed" and the help text gains a dedicated `pr subcommands` table and a `pr AI options` table so the new surface is discoverable from `--help` alone.
- **`CheckForUpdates` no longer renders a yellow Panel** that can interleave with the AnsiConsole.Status() spinner. The check now runs synchronously up-front and emits a single-line `⬆  yp <version> available` hint. The cache lookup is sub-100ms so the perceived startup latency is unchanged.
- **`yp --help`** adds a `--no-spinner` row under "Global flags" and the `commit` and `pr` option tables.
- **`yp --help` `setup` row** now mentions the TUI + `--wizard` escape hatch, and the example section adds `yp setup`, `yp setup --wizard`, and `yp setup --tui` lines so the keyboard shortcuts (`←/→`, `↑/↓`, `T`, `Enter`, `Esc`) are discoverable from `--help` alone.

### 🐛 Fixed (more)
- **BUG-001 / BUG-002 / BUG-003** (issue #18) — `Custom.EsfuerzoRealHH` vs `Custom.EsfuerzoReal` refname routing is now project-aware via `AzDevOpsFieldRefNameResolver`; the `ValidAzureStates` constant is gone (states are now resolved dynamically per project); the `Done` transition pre-flight blocks the PATCH if `Evidencias de finalización` is empty. Resolves #18.
- **Spectre.Console markup crash in `yp --help` on the `diff` table** (pre-existing, surfaced by the new diff subcommand shipped in v9): the JSON example contained literal `[...]` markup tags that crashed the markup tokenizer. Now rendered as a plain-text summary.
- **Spectre.Console crash in `hu show` / `hu list`** when stdout is redirected (the trailing interactive prompt is now auto-skipped in non-interactive contexts).
- **`SKILL.md` was missing the YAML frontmatter required by the [Agent Skills spec](https://agentskills.io/specification)** — `npx skills add elvisbrevi/yitpush` reported `No valid skills found. Skills require a SKILL.md with name and description.`, so the skill was silently unindexed on skills.sh. Both `SKILL.md` (root) and `skills/yp/SKILL.md` (installable, byte-identical via the existing `SkillFileAlignmentTests.RootSkillMd_and_installable_skillMd_are_byte_identical` test) now lead with `---\nname: yp\ndescription: AI-powered Git commit, PR description, and Azure DevOps management via the yp CLI.\nlicense: MIT\nmetadata:\n  version: "2.3.0"\n  install: dotnet tool install -g YitPush\n  invoke: yp <command> [options]\n---`. The redundant `## Skill Metadata` block is removed. New `SkillFileAlignmentTests.SkillMd_starts_with_valid_yaml_frontmatter` asserts the frontmatter exists, parses, and contains a non-empty `description` ≤1024 chars. `SkillMd_frontmatter_description_mentions_key_capabilities` asserts the description includes `commit`, `PR`, and `Azure DevOps` so agents can match user requests to the skill. `InstallableSkillMd_frontmatter_name_matches_parent_directory` enforces the spec rule `name == parent-dir-name` (`yp` for the installable copy). After the fix, `npx skills add elvisbrevi/yitpush --skill yp --all -y` reports `1 skill found` (verified post-merge).
- **`scripts/release-2.3.0.sh` push step was globbing `./nupkg/`** — the pre-build step `--skip-duplicate` made the failure silent: a 409 on `YitPush.2.1.2.nupkg` and `YitPush.2.1.1.nupkg` (already on nuget.org) was logged but the current `YitPush.2.3.0.nupkg` was never pushed. The push command now uses an explicit version-specific path (`./nupkg/YitPush.${VERSION}.nupkg`) instead of a directory glob, so a stray stale artifact can never silently skip the current version. Belt-and-braces: a new pre-build cleanup step (`rm -f nupkg/*.nupkg nupkg/*.symbols.nupkg`) wipes stale artifacts before pack. The `nupkg/` directory is build output and not git-tracked (`git ls-files nupkg/` is empty), so `rm -f` is safe. A new pre-flight check `[NUGET_API_KEY]` fails fast with a clear error if the env var is unset (previously: cryptic 401 from nuget.org). New `ReleaseScriptTests.ReleaseScript_push_command_uses_version_specific_path_not_directory_glob` + `ReleaseScript_cleanup_uses_aggressive_nupkg_pattern` + `ReleaseScript_checks_NUGET_API_KEY_in_preflight` + `ReleaseScript_git_tag_step_is_idempotent` + `ReleaseScript_version_can_be_overridden_via_env_var` lock all of this in. Resolves #4 (release-script follow-up).

### 📝 Notes
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

## 2.1.x — 2026

### [2.1.8] - 2026-05-13
- **Fixed**: **Task description format** — Descriptions now preserved as raw Markdown via Azure DevOps REST API. Newlines, headers, lists, and formatting render correctly since the API stores JSON with proper `\n` encoding. No conversion needed — Azure DevOps renders Markdown natively.

### [2.1.7] - 2026-05-13
- **Added**: `--no-link` / `-n` flag for `hu task` (skip branch-linking prompt) and `--repo` + `--branch` auto-link (with both flags, tasks link to the branch automatically).
- **Fixed**: Markdown task descriptions now converted to HTML and stored in `Microsoft.VSTS.Common.DescriptionHtml` so they render correctly in Azure DevOps.

### [2.1.6] - 2026-05-13
- **Fixed (docs)**: `--task-titles` / `-t` flag was supported but undocumented; now in CLI help and the agent skill file.

### [2.1.5] - 2026-05-13
- **Fixed**: Non-interactive task creation — `--task-titles` / `-t` flag added so piped descriptions don't fail with `Failed to read input in non-interactive mode`.

### [2.1.4] - 2026-04-29
- **Added**: Interactive search/filtering in all significant selection lists (Azure DevOps orgs/projects/repos/HUs/tasks/variable groups, git branch selection, setup provider/model lists, main Azure DevOps menu).

### [2.1.3] - 2026-04-28
- **Fixed**: `[Custom...]` option no longer crashes `yp setup` — entry escaped for Spectre.Console markup.

### [2.1.2] - 2026-04-27
- **Fixed**: `yp setup` model selection no longer crashes on model names containing `[`, `]`, `<`, `>`, `&` — escaped before rendering in `SelectionPrompt`.

### [2.1.0] - 2026-04-27
- **Added**: Live model discovery in `yp setup` via each provider's `/models` endpoint.
  - OpenAI / DeepSeek: `GET /v1/models` with `Authorization: Bearer` (filtered to `gpt-*`, `o1`, `o3`, `o4`, `chatgpt-*`).
  - OpenRouter: `GET /api/v1/models` (no auth); honors custom base URL.
  - Anthropic: `GET /v1/models?limit=100` with `x-api-key` + `anthropic-version`.
  - Google Gemini: `GET /v1beta/models?key=…`, filtered to `generateContent`-supporting models.
- **Added**: Models cache at `~/.yitpush/models-cache.json` with 24h TTL.
- **Added**: Live/defaults indicator in the model selection title.
- **Added**: `CLAUDE.md` for Claude Code guidance.
- **Changed**: Model selection paginates at 15 entries to handle OpenRouter's 100+ models.
- **Changed**: `[Custom…]` remains available in every provider's menu as an escape hatch.

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

## 1.x — 2025

### [1.4.0] - 2026-03-14
- **Added**: Command shortened to **`yp`** for faster usage (`yitpush` alias still supported).
- **Added**: **Esfuerzo Real HH** field support in `task update` (`--effort-real` / `-er`).
- **Added**: Full Azure DevOps Task Management: `hu list`, `task show`, `task update`.
- **Added**: Interactive and direct updates for Effort, Esfuerzo Real HH, Remaining Work, State, and Comments.
- **Added**: Smart state validation and selection menus for Azure DevOps.
- **Added**: Integrated "List all fields" tool for debugging work item technical names.
- **Improved**: Faster navigation with `← Back` support in all interactive menus.
- **Improved**: Sorting by ID descending (recency first) in all Azure DevOps lists.

### [1.3.0] - 2026-02-01
- **Added**: `yp azure-devops link` — add branch/commit/PR links to any work item.
- **Added**: `yp azure-devops hu link` — link a repository branch to a User Story using ArtifactLink (shows in Azure Boards Development section).

### [1.2.0] - 2026-01-15
- **Added**: `yp azure-devops hu show` — show User Story details (title, effort, description, links).
- **Added**: `yp azure-devops task show` — show Task details.
- **Added**: `yp azure-devops hu task` — create tasks for a User Story interactively or via CLI args (quick mode).

### [1.1.0] - 2025-12-01
- **Added**: `yp pr` — generate pull request descriptions between two branches using AI.
- **Added**: `--detailed` flag for both `commit` and `pr` commands.
- **Added**: `--language` / `--lang` flag for `commit` and `pr` commands.
- **Added**: `--save` flag to write output to a markdown file.
- **Added**: Interactive branch selection with pagination.

### [1.0.0] - 2025-11-01
- **Added**: Initial release.
- **Added**: `yp commit` — AI-generated commit messages using DeepSeek.
- **Added**: `yp checkout` — interactive branch checkout.
- **Added**: `yp azure-devops repo new` — create Azure DevOps repositories.

---

> 🗂️ **Esto es solo un resumen — revisa cada sección arriba para el detalle completo de los cambios.** Si encuentras algo que falta, abre un issue o PR en https://github.com/elvisbrevi/yitpush/issues.
