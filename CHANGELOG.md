# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

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
