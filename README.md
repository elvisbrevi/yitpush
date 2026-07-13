# 🚀 yp (YitPush)

AI-Powered Git Commit and Azure DevOps Management Tool — now with multi-provider AI support.

## 🛠️ Installation

```bash
# Clone the repository
git clone https://github.com/elvisbrevi/yitpush.git
cd yitpush

# Build and install globally
dotnet pack -c Release
dotnet tool install --global --add-source ./nupkg YitPush
```

> **Note:** The command is **`yp`**. If you've previously installed it as `yitpush`, uninstall it first: `dotnet tool uninstall -g YitPush`.

After installing, run `yp setup` to configure your AI provider. The setup wizard will also offer to add the `yitpush` alias to your shell automatically (`.zshrc`, `.bashrc`, or `.profile` depending on your OS).

### Install as an Agent Skill

If you use Claude Code, Cursor, Gemini CLI, or any [Agent Skills](https://agentskills.io)-compatible agent, install the `yp` skill so your agent knows how to use it automatically.

Via `yp` (recommended — also offered during `yp setup`):
```bash
yp skill
```

Or directly with the skills CLI:
```bash
npx skills add elvisbrevi/yitpush
```

Listed on [skills.sh/elvisbrevi/yitpush](https://skills.sh/elvisbrevi/yitpush).

## 🔑 Configuration

### Interactive Setup (Recommended)
Run the setup TUI to configure your preferred AI provider:
```bash
yp setup                # launches the TUI (default in a real terminal)
yp setup --wizard       # forces the legacy 5-step wizard (auto-selected in CI)
yp setup --tui          # forces the TUI even if the routing would otherwise pick --wizard
```

The **TUI** shows a two-column live layout: the left column lists the providers (with a green dot for the ones you've already tested), the right column shows the API key status, endpoint, and the model picker. Keyboard: `←/→` switch provider, `↑/↓` switch model, `T` test connection, `Enter` save, `Esc` cancel. The TUI auto-falls-back to the wizard when stdin/stdout is redirected (CI), so scripts and pipes keep working without any change.

The **legacy wizard** (`--wizard`) guides you through:
1. Selecting a provider: **OpenAI**, **Anthropic**, **Google Gemini**, **DeepSeek**, **OpenRouter**, or **NVIDIA NIM**
2. Entering your API key (masked input)
3. Selecting a model from a curated list or entering a custom one
4. Validating the key with a test call
5. Saving to `~/.yitpush/config.json`

### Manual / Environment Variable (Backward Compatible)
You can still use environment variables. If no config file is found, `DEEPSEEK_API_KEY` is used automatically:
```bash
export DEEPSEEK_API_KEY='your-api-key-here'
```

Other supported environment variables (override the config file):
```bash
export OPENAI_API_KEY='...'
export ANTHROPIC_API_KEY='...'
export GOOGLE_API_KEY='...'
export DEEPSEEK_API_KEY='...'
export OPENROUTER_API_KEY='...'
```

## 📖 Usage

### Git Commands
| Command | Description |
|---------|-------------|
| `yp setup` | Configure your AI provider in the TUI (pass `--wizard` for the legacy 5-step flow) |
| `yp skill` | Install the yp skill for your AI agent |
| `yp commit` | Stage, commit and push with an AI-generated message |
| `yp checkout` | Interactive branch checkout |
| `yp pr` | Generate a pull request description between two branches |
| `yp diff` | Show working-tree or branch-to-branch diff (supports `--files`, `--hunks`, `--stat`, `--json`) |
| `yp --version` / `yp -V` | Print the assembly version (e.g. `2.3.0`) and exit 0 — safe to pipe |

#### `yp diff` — friendly diff viewer

`yp diff` wraps `git diff` with flags for the common agent and human workflows. The `--json` output is stable and safe to pipe into `jq` or any other tool:

```bash
yp diff                                       # working-tree diff, syntax-colored
yp diff --files                               # just the file list with +N -M per file
yp diff --hunks                               # changed lines only, no context (git diff -U0)
yp diff feature/abc main --stat               # branch-to-branch stat
yp diff --json | jq '.files[0].hunks[0].afterLine'        # first added line of the first hunk
yp diff --json | jq '.files[] | select(.additions > 0) | .path'  # only added paths
```

Flags: `--files` / `--stat` (file list), `--hunks` (changed lines only), `--json` (stable JSON: `{ files: [{ path, oldPath?, additions, deletions, isBinary, isRename, hunks: [{ beforeLine, afterLine, content }] }] }`), `--no-color` (auto-applied when stdout is redirected), and an optional `<refA> <refB>` positional pair for `git diff <refA>..<refB>`.

### Azure DevOps Commands
| Command | Description |
|---------|-------------|
| `yp azure-devops` | Enter interactive Azure DevOps menu |
| `yp azure-devops repo new` | Create a new repository |
| `yp azure-devops repo checkout` | Clone/Checkout a repository |
| `yp azure-devops variable-group list` | List and inspect variable groups |
| `yp azure-devops hu task` | Create tasks for a User Story |
| `yp azure-devops hu list` | List tasks of a User Story and manage them |
| `yp azure-devops hu show` | Show details of a User Story (title, effort, links) |
| `yp azure-devops task show` | Show details of a Task (effort, remaining, description) |
| `yp azure-devops task update` | Update task fields (effort, state, comments) |
| `yp azure-devops task delete` | Move a work item to the recycle bin (prompts by default; pass `--yes` in CI) |
| `yp azure-devops task attach` | Upload a local file as an `AttachedFile` on a work item |
| `yp azure-devops hu link` | Link a repository branch to a User Story |
| `yp azure-devops link` | Add a link (branch/commit/PR) to any work item — supports `--repo <r> --branch <b>` quick mode for both HUs and Tasks |

#### 🧰 Stable Output for Agents and Scripts
The `hu show`, `task show`, and `hu list` commands accept a `--json` flag that emits a flat JSON object on stdout (no ANSI escapes), so the output is safe to pipe into `jq`:

```bash
yp azure-devops hu show MyOrg 12345 --json | jq '.title'
yp azure-devops task show MyOrg 67890 --json | jq '.state'
yp azure-devops hu list MyOrg MyProj 12345 --json | jq '.value | length'
```

When stdout is piped, the trailing interactive prompt is automatically skipped, so `yp ... | jq ...` never crashes with the Spectre "isn't interactive" error.

## 🚀 Detailed Features

### 🤖 Multi-Provider AI Support

`yp` supports six AI providers. Run `yp setup` to switch between them at any time.

| Provider | Models |
|----------|--------|
| OpenAI | gpt-4o, gpt-4o-mini, o1, o1-mini, ... |
| Anthropic | claude-opus-4-6-20250514, claude-sonnet-4-6-20250514, claude-haiku-4-5-20251008 |
| Google Gemini | gemini-2.0-flash, gemini-1.5-pro, ... |
| DeepSeek | deepseek-chat, deepseek-reasoner |
| OpenRouter | 100+ models (google/gemini-2.0-flash, openai/gpt-4o, ...) |
| NVIDIA NIM | meta/llama-3.1-70b-instruct, nvidia/nemotron-4-340b-instruct, ... |

The `NVIDIA_API_KEY` environment variable overrides the stored API key for the NVIDIA NIM provider (same convention as `OPENAI_API_KEY`, `ANTHROPIC_API_KEY`, etc.).

### 📝 Smart Commits
`yp commit` analyzes your staged changes and generates a professional commit message. With `--conventional` the output follows the `<type>(<scope>)?!?: <subject>` format; with `--amend` the last commit is rewritten in place from `git diff HEAD~1`.

```bash
yp commit                                # Auto commit and push
yp commit --confirm                      # Review before committing
yp commit --detailed                     # Generate title + body
yp commit --language spanish             # Output in Spanish
yp commit -l french                      # Short flag for language
yp commit --conventional --type feat --scope wcf           # Conventional Commits, forced type/scope
yp commit --conventional --detect-breaking                # Conventional + scan diff for breaking changes
yp commit --amend                        # Regenerate last commit's message (no push)
yp commit --template ~/.yitpush/commit.md                 # Render output through a custom template
```

| Flag | Description |
|------|-------------|
| `--confirm` | Ask for confirmation before committing |
| `--detailed` | Generate detailed commit with title + body |
| `--language <lang>`, `--lang`, `-l` | Output language (default: english) |
| `--save` | Save commit message to a markdown file |
| `--conventional` | Format as Conventional Commits `<type>(<scope>)?!?: <subject>` (with optional `BREAKING CHANGE:` footer) |
| `--type <feat\|fix\|chore\|refactor\|docs\|test\|perf\|build\|ci\|style>` | Force the Conventional Commits type (overrides AI inference) |
| `--scope <scope>` | Force the Conventional Commits scope (e.g. `api`, `wcf`) |
| `--detect-breaking` | Scan the diff for breaking changes (removed C# public symbol, removed TS/JS export, JSON major bump, `#major.bump` marker) and feed them to the AI / append as footer |
| `--amend` | Regenerate the LAST commit's message from `git diff HEAD~1` via `git commit --amend` — no `git add`, no push |
| `--template <path-to-md>` | Render the AI output through a Handlebars-ish template (`{{type}}`, `{{scope}}`, `{{subject}}`, `{{body}}`, `{{refs}}`); exits 6 if the file is missing |

The default commit format can also be set per project in `~/.yitpush/config.json` under the `"commitFormat"` key (`"conventional"`, `"plain"`, `"gitmoji"`, or a path to a template file). An explicit `--conventional` or `--template` flag on a single invocation overrides the stored default.

### 📋 Pull Request Management
Triage and act on Azure DevOps PRs from the terminal. Running `yp pr` with no args opens an interactive menu with six options: generate an AI description, list, show, comments, reply, or create a PR.

```bash
yp pr                                                       # interactive menu
yp pr list                                                  # list open PRs
yp pr show 12345                                            # show PR details (title, description, reviewers)
yp pr comments 12345                                        # list discussion threads with file/line context
yp pr reply 12345 678 --body "Fixed in commit abc"         # reply to a thread (REST API)
yp pr create --source feature/x --target main --title "feat: x" --body-file desc.md  # open a PR (REST API)
yp pr create --source feature/x --target main --title "feat: x" --body-file desc.md --auto-complete  # + auto-complete
yp pr --detailed                                            # AI description (backward compat)
```

| Subcommand | Description |
|-----------|-------------|
| `list` | List open PRs in a Spectre table — `--json` for a stable envelope (`{ exitCode, count, pullRequests: [...] }`) |
| `show <pr-id>` | Show title, description, source/target, status, author, reviewers — `--json` supported |
| `comments <pr-id>` | List all discussion threads (REST API: `az repos pr thread` does not exist) with author, date, body, file path, line — `--json` supported |
| `reply <pr-id> <thread-id> --body "..."` | Post a reply to a specific thread via the REST API — `--json` supported |
| `create --source <b> --target <b> --title <t> [--body-file <path>] [--auto-complete]` | Open a new PR via the REST API so the description can be larger than `az`'s argv limit. Without `--body-file` the description is read from stdin. `--auto-complete` sets `completionOptions` (`deleteSourceBranch: true`). `--json` supported |

**Exit codes**: `0` success, `1` not-found (404), `2` auth/error, `3` validation.

Read operations (`list`, `show`) go through `az repos pr` so the CLI's auth + defaults are reused; write operations (`reply`, `create`) and threads (`comments`) go through the Azure DevOps REST API so the description can be larger than `az`'s argv limit and because `az repos pr thread` does not exist.

The original AI-description flow (`yp pr --detailed -l spanish --save`) is preserved as a backward-compatible escape hatch.

### 🔷 Azure DevOps Integration

#### 📋 Task Management
- **List & Update**: Use `yp azure-devops hu list` to see all tasks of a HU. Select a task to:
    - View full **Description**.
    - See **Effort (HH)**, **Esfuerzo Real (HH)** and **Remaining Work**.
    - Inspect **Links** (Branches, Commits, PRs).
    - **Update** fields interactively.

#### ⚡ Quick Mode (CLI)
Update work items directly from your terminal:
```bash
# Update multiple fields
yp azure-devops task update <org> <id> --state "Active" --effort-real "5" --remaining "2"

# Add a comment (posts to the Discussion tab; use --history for the legacy History field)
yp azure-devops task update <org> <id> --comment "Progress update: logic refactored"

# Edit title, description, or evidence (project-specific "Evidencias de finalización" field, resolved by name)
yp azure-devops task update <org> <id> --title "New title" --description "New description"
yp azure-devops task update <org> <id> --evidence "curl http://localhost:3333/api/v1/comunas ..."

# Reassign the task (accepts UPN, display name, or empty to clear)
yp azure-devops task update <org> <id> --assigned-to "elvis.brevi@sag.gob.cl"
yp azure-devops task update <org> <id> --assigned-to "Elvis Brevi"
yp azure-devops task update <org> <id> --assigned-to ""

# Create tasks with info
yp azure-devops hu task <org> <proj> <hu-id> --description "Task info" --effort "4" --task-titles "Desarrollo, Pruebas Unitarias" --no-link
```

#### 🔗 Deep Linking
Link your local branch to an Azure DevOps work item natively:
```bash
# Link a User Story to a branch
yp azure-devops hu link <org> <proj> <id> --repo <name> --branch <name>

# Link any work item (HU or Task) to a branch — quick mode (issue #9)
yp azure-devops link <org> <proj> <id> --repo <name> --branch <name>
```
This uses `ArtifactLink`, making the branch appear in the **Development** section of the Azure Boards UI. When the target work item is a Task, the `Custom.URLCommit` field is also written as a navigation fallback for legacy `az` scripts (no-op if the field doesn't exist). Omit `--repo` / `--branch` to fall back to the interactive picker.

## 📝 Navigation
- Every interactive menu includes a **`← Back`** option.
- All lists (HUs, Tasks, Projects, Repos) are sorted by **Recency First** (ID Descending).
- **Auto-detection**: The tool automatically detects your organizations and projects.

---
*Created with ❤️ by Elvis Brevi*
