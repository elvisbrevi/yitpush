# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

---

## [Unreleased]

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
