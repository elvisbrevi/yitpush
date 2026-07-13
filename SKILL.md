# yp (YitPush) — Agent Skill

> The installable skill package is at [`skills/yp/SKILL.md`](skills/yp/SKILL.md) — compatible with the [Agent Skills open standard](https://agentskills.io) and listed on [skills.sh](https://skills.sh).
>
> Install with: `npx skills add elvisbrevi/yitpush`

This file is a human-readable reference. The machine-readable skill follows below.

## Skill Metadata

- **Name**: yp
- **Description**: AI-powered Git commit automation and Azure DevOps management CLI
- **Version**: 2.0.0
- **Install**: `dotnet tool install -g YitPush`
- **Invoke**: `yp <command> [options]`

---

## Tool Definitions

### tool: commit

Stages all changes, generates an AI commit message, commits and pushes.

```
yp commit [flags]
yp commit --amend [flags]
```

**When to use**: User wants to commit and push changes with an AI-generated message. With `--amend`, the AI re-runs against `git diff HEAD~1` and rewrites the last commit (no push).

**Parameters**:
- `--confirm` — pause and ask user to approve the message before committing
- `--detailed` — generate a commit with a subject line + body paragraph
- `--language <lang>` / `-l <lang>` — language for the output (e.g., spanish, french, portuguese)
- `--save` — write the commit message to a `.md` file in the current directory
- `--conventional` — format the message as a Conventional Commits `<type>(<scope>)?!?: <subject>` (optionally with a `BREAKING CHANGE:` footer)
- `--type <feat|fix|chore|refactor|docs|test|perf|build|ci|style>` — force the Conventional Commits type (overrides the AI's inference)
- `--scope <scope>` — force the Conventional Commits scope (e.g. `api`, `wcf`)
- `--detect-breaking` — scan the diff for breaking changes (removed public symbol in C#, removed export in TS/JS, JSON major-version bump, `#major.bump` markers) and feed them into the AI prompt; with `--conventional` the first detected marker is also appended as a `BREAKING CHANGE:` footer
- `--amend` — regenerate the last commit's message via `git commit --amend`; skips the working-tree flow and does NOT push
- `--template <path-to-md>` — render the AI output through a Handlebars-ish template (`{{type}}`, `{{scope}}`, `{{subject}}`, `{{body}}`, `{{refs}}`); missing variables resolve to an empty string, malformed tokens (`{{1abc}}`, `{{a.b}}`, `{{}}`) are left untouched; the tool exits `6` when the file is missing
- `--no-spinner` — skip the AnsiConsole.Status() spinner that wraps the AI call and the `git push`; the operation runs inline and the exit code is preserved. Same effect as setting `YITPUSH_NO_SPINNER=1` (or `true`/`yes`) in the environment. The spinner is also auto-disabled when stdout is redirected (CI logs).

**Examples**:
```bash
yp commit
yp commit --confirm
yp commit --detailed -l spanish
yp commit --conventional --type feat --scope wcf
yp commit --conventional --detect-breaking
yp commit --amend
yp commit --template ~/.yitpush/commit-template.md
```

The default commit format can also be set per project in `~/.yitpush/config.json` under the new `"commitFormat"` key (`"conventional"`, `"plain"`, `"gitmoji"`, or a template file path). An explicit `--conventional` or `--template` flag on a single invocation overrides the stored default.

---

### tool: pr

Triage and act on Azure DevOps pull requests from the terminal, or generate an AI-powered PR description for the current branch.

```
yp pr                                     # interactive menu (default)
yp pr list                                # list open PRs in the current repo
yp pr show <pr-id>                        # show a PR's details
yp pr comments <pr-id>                    # list discussion threads (REST API)
yp pr reply <pr-id> <thread-id> --body "..."   # reply to a thread (REST API)
yp pr create --source <b> --target <b> --title <t> [--body-file <path>] [--auto-complete]  # open a PR via REST API
yp pr [--detailed] [--language <lang>] [--save]   # AI-generated description (backward compat)
```

**When to use**:
- User wants to act on a PR from the terminal without opening a browser
- User wants to list/show/comment/reply to PRs from CI scripts (use `--json` for a stable envelope)
- User wants to open a new PR with a description larger than `az`'s argv limit (use `yp pr create --body-file ...`)

**Subcommands**:
- `yp pr list` — lists open PRs in the current repo. Output: Spectre table (id, title, author, source, target, draft, created). `--json` emits `{ exitCode, count, pullRequests: [...] }`.
- `yp pr show <pr-id>` — shows title, description, source/target, status, author, and the reviewer table. `--json` emits `{ exitCode, pullRequest: { id, title, description, ..., reviewers: [...], changedFiles: [...] } }`.
- `yp pr comments <pr-id>` — shows all discussion threads with author, date, body, and file/line context. Uses the REST API (`az repos pr thread` does not exist). `--json` emits `{ exitCode, prId, threads: [...] }`.
- `yp pr reply <pr-id> <thread-id> --body "..."` — posts a reply to a specific thread. REST API. `--json` emits `{ exitCode, prId, threadId, error? }`.
- `yp pr create --source <branch> --target <branch> --title <title> [--body-file <path>] [--auto-complete]` — opens a PR via REST API. If `--body-file` is not provided, the description is read from stdin. `--auto-complete` sets `completionOptions` (with `deleteSourceBranch: true`). `--json` emits `{ exitCode, pullRequestId, error? }`.

**Exit codes**: `0` success, `1` not-found (404), `2` auth/error, `3` validation.

**Backward-compat AI mode**: `yp pr` with no args now opens the interactive menu. To force the original AI description generator, pass `--detailed` or any AI flag (e.g. `yp pr --detailed -l french --save`).

**Examples**:
```bash
yp pr                                              # interactive menu
yp pr list                                         # all open PRs
yp pr show 12345                                   # one PR's details
yp pr comments 12345                               # all threads on a PR
yp pr reply 12345 678 --body "Fixed in commit abc" # reply to a thread
yp pr create --source feature/x --target main --title "feat: x" --body-file desc.md
yp pr create --source feature/x --target main --title "feat: x" --body-file desc.md --auto-complete
yp pr list --json | jq '.pullRequests[].id'        # machine-readable list
yp pr --detailed -l spanish                        # AI description (backward compat)
```

---

### tool: diff

Friendly wrapper over `git diff` for the common agent and human workflows. Wraps the working-tree diff (default: `git diff HEAD`) with syntax-friendly coloring (`+` lines green, `-` lines red via Spectre.Console `Markup`), or emits a structured JSON payload for piping to other tools.

```
yp diff [--files] [--hunks] [--stat] [--json] [--no-color] [<refA> <refB>]
```

**When to use**: User wants to inspect a diff without going through AI generation. Use `--json` when the consumer is another tool (`jq`, a CI script, an LLM that needs structured input) — the JSON shape is stable: `{ files: [{ path, oldPath?, additions, deletions, isBinary, isRename, hunks: [{ beforeLine, afterLine, content }] }] }`.

**Parameters**:
- (no flag) — print the working-tree diff with `+`/`-` coloring, exit 0
- `--files` / `--stat` — print only the list of changed files with `+N -M` per file (a colorful `git diff --stat`)
- `--hunks` — print only the changed lines, no context (`git diff -U0` semantics)
- `<refA> <refB>` — diff branch/tag/commit to another (`git diff <refA>..<refB>`)
- `--json` — emit the stable JSON shape on stdout (no ANSI escapes); `--no-color` is auto-applied when stdout is redirected so the output is pipeline-friendly
- `--no-color` — disable Spectre coloring manually

**Examples**:
```bash
yp diff                                         # working-tree diff, colored
yp diff --files                                 # just the file list
yp diff --hunks                                 # changed lines only
yp diff feature/abc main --stat                 # branch-to-branch stat
yp diff --json | jq '.files[0].hunks[0].afterLine'   # pipe the diff to jq
yp diff --json | jq '.files[] | select(.additions > 0) | .path'   # added-only paths
```

---

### tool: setup

Configures the active AI provider interactively.

```
yp setup                # launches the TUI (default in a real terminal)
yp setup --wizard       # forces the legacy 5-step wizard (auto-selected in CI)
yp setup --tui          # forces the TUI even if the routing would otherwise pick --wizard
```

**When to use**: First-time setup or when changing the AI provider or API key.

**TUI flow (default)**: a two-column live layout — left column lists the providers (with a green dot for the ones you've already tested), right column shows the API key status, endpoint, and the model picker. Keyboard: `←/→` switch provider, `↑/↓` switch model, `T` test connection, `Enter` save, `Esc` cancel. The TUI auto-falls-back to the legacy wizard when stdin/stdout is redirected (e.g. CI), so scripts don't need to be updated.

**Wizard flow (--wizard)**: select provider → enter API key → select model → validate → save to `~/.yitpush/config.json`.

Supported providers: **OpenAI**, **Anthropic**, **Google Gemini**, **DeepSeek**, **OpenRouter**, **NVIDIA NIM**. Each provider has a `<PROVIDER>_API_KEY` environment variable that overrides the stored key at runtime (e.g. `NVIDIA_API_KEY` for the NVIDIA NIM provider).

---

### tool: checkout

Interactive branch selector.

```
yp checkout
```

**When to use**: User wants to switch to a different git branch interactively.

---

### tool: skill

Installs the `yp` agent skill so any Agent Skills-compatible AI agent (Claude Code, Cursor, Gemini CLI, etc.) knows how to invoke `yp`. Internally runs `npx skills add elvisbrevi/yitpush`.

```
yp skill
```

**When to use**: User wants their AI agent to learn how to drive `yp`, or the setup wizard offered to install it later.

---

### tool: azure-devops

Manages Azure DevOps resources. Run without arguments for interactive menu, or pass subcommands directly.

```
yp azure-devops [subcommand] [args] [flags]
```

**Key subcommands**:

| Subcommand | Purpose |
|-----------|---------|
| `repo new` | Create a new repository interactively |
| `repo checkout` | Clone/checkout a repository interactively |
| `variable-group list` | List and inspect variable groups |
| `hu show <org> <id>` | Show User Story details |
| `hu list <org> <proj> <id>` | List tasks of a User Story |
| `hu task <org> <proj> <id>` | Create tasks for a User Story |
| `hu task flags` | `--description|-d`, `--effort|-e`, `--task-titles|-t`, `--no-link|-n`, `--repo`, `--branch` |
| `hu link <org> <proj> <id> --repo <r> --branch <b>` | Link a branch to a User Story |
| `task show <org> <id>` | Show task details |
| `task update <org> <id> [flags]` | Update task fields (alias: `hu update`, `wi update`) |
| `task delete <org> <id> [--yes\|-y]` | Move work item to the recycle bin (prompts by default; pass `--yes` in CI) |
| `task attach <org> <project> <id> <file-path> [--comment <text>]` | Upload a local file as an `AttachedFile` relation on the work item |
| `link <org> <proj> <id> [--repo <r> --branch <b>]` | Add a link (branch/commit/PR) to any work item; `--repo` + `--branch` skip the menus and create the ArtifactLink in quick mode (same as `hu link`) |

**task update flags**: `--title`, `--description|-D`, `--evidence`, `--field <RefName=val>`, `--effort|-e`, `--effort-real|-er`, `--remaining|-r`, `--state|-s`, `--comment|-c`, `--assigned-to <upn|display-name|"">`, `--history`
**hu link flags**: `--repo`, `--branch`
**link flags**: `--repo`, `--branch` (when both are provided the interactive menu is skipped, matching `hu link` quick mode; the `Custom.URLCommit` field is also written as a navigation fallback for legacy `az` scripts)

Note: `--comment` posts a discussion comment; `--history` writes the legacy History field; `--assigned-to` resolves UPN or display name against the project's identity store (multiple matches exit 4 with a candidate list, empty string clears the assignment).

**Pre-flight check (Done transition, v2.3.0):** when `--state "Done"` is passed **without** `--evidence`, `yp` first `GET`s the work item and verifies that `Evidencias de finalización` (resolved via the project-specific refname) is non-empty. If the field is empty, the tool prints `❌ Missing required field for Done transition: Evidencias de finalización (refname Custom.<GUID>). Re-run with --evidence "<text>".` and exits 2 without sending the PATCH. The check is skipped when (a) the target state is not `Done`, (b) `--evidence` is provided (the flag itself populates the field), or (c) the work item already has a non-empty `Evidencias de finalización` value. Any pre-flight failure (auth, network, 5xx, unresolvable refname) is treated as fail-open so the existing PATCH flow is not regressed.

---

## Global Flags

```
yp --version    # prints the assembly version (e.g. "2.3.0") and exits 0
yp -V           # short alias
yp --help       # prints the help table
```

These work without a subcommand. `yp --version` is intended for scripts and CI; its output has no ANSI escapes.

---

## Stable JSON Output

`hu show`, `task show`, and `hu list` accept a `--json` flag that emits a flat JSON object on stdout instead of a Spectre.Console table. The shape is stable enough to pipe to `jq`:

```bash
yp azure-devops hu show MyOrg 12345 --json | jq '.title'
yp azure-devops task show MyOrg 67890 --json | jq '.state'
yp azure-devops hu list MyOrg MyProj 12345 --json | jq '.value | length'
```

`hu show` / `task show` emit a single flat object with `id`, `type`, `title`, `state`, `assignedTo`, `createdDate`, `areaPath`, `iterationPath`, `effort`, `effortReal`, `remaining`, `month`, `urlCommit`, `description`, and (when present) `relations`. Unknown custom fields are passed through with their full refname (e.g. `Custom.Foo`).

`hu list` emits `{"huId": "...", "value": [{id, title, state}, ...]}` so `jq '.value | length'` returns the number of child tasks.

When stdout is piped, the interactive follow-up prompt at the end of `hu show` / `hu list` is automatically skipped, so non-interactive invocations never crash with the Spectre "isn't interactive" error.

---

## Usage Instructions for Gemini CLI

When the user asks you to:

- **"commit my changes"** → run `yp commit`
- **"commit and let me review"** → run `yp commit --confirm`
- **"generate a PR description"** → run `yp pr`
- **"switch branch"** → run `yp checkout`
- **"show user story 12345"** → run `yp azure-devops hu show <org> 12345`; append `--json` when the user wants machine-readable output
- **"update task 67890 state to Doing"** → run `yp azure-devops task update <org> 67890 --state "Doing"`
- **"configure AI provider"** → run `yp setup`
- **"install the yp skill"** → run `yp skill`
- **"list variable groups"** → run `yp azure-devops variable-group list`
- **"create a new repo"** → run `yp azure-devops repo new`
- **"clone a repo"** → run `yp azure-devops repo checkout`
- **"link a branch to work item 67890"** → run `yp azure-devops link <org> <proj> 67890`; add `--repo <r> --branch <b>` to skip the menus (quick mode)
- **"what version of yp is installed"** → run `yp --version`

## Notes

- `yp` requires a configured AI provider (`yp setup`) or the `DEEPSEEK_API_KEY` env var
- All interactive menus support `← Back` navigation
- Version updates are shown automatically when a new version is available on NuGet
