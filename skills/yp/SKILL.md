---
name: yp
description: AI-powered git workflow and Azure DevOps work-item management via the `yp` (YitPush) CLI. Use this skill whenever the user wants to commit and push code, generate a commit message, draft a pull request description, switch git branches interactively, configure an AI provider, or interact with Azure DevOps — including showing/listing/creating/updating user stories and tasks, creating or cloning repos, linking branches/commits/PRs to work items, listing variable groups, or installing the yp agent skill itself. Trigger even when the user does not say "yp" — phrases like "commit my changes", "push this", "make a PR description", "switch branch", "create tasks for HU 12345", "update task state to Doing", "link this branch to the user story", "show me HU 12345", "list tasks of the user story", "create an Azure DevOps repo", or "configure the AI provider" should all activate this skill.
license: MIT
---

# yp — AI Git & Azure DevOps CLI

`yp` is a .NET global CLI that turns one-liner intents into git or Azure DevOps actions, using a configurable AI provider (OpenAI, Anthropic, Google Gemini, DeepSeek, or OpenRouter) for natural-language artifacts like commit messages and PR descriptions.

This document is the contract between an agent and `yp`: pick the right command for the user's intent, fill in arguments, and execute. Prefer non-interactive ("quick mode") forms whenever the user has provided enough context — agents drive `yp` better when no menus are involved.

## Prerequisites and prelaunch checks

Before running any `yp` command, confirm three things in this order. If any check fails, fix it before continuing.

1. **`yp` is installed.** Run `yp --help`. If the binary is missing, install with `dotnet tool install -g YitPush` (requires .NET 10 SDK or runtime).
2. **An AI provider is configured** (only required for `commit` and `pr`). Look for `~/.yitpush/config.json`, or one of the env vars `OPENAI_API_KEY`, `ANTHROPIC_API_KEY`, `GOOGLE_API_KEY`, `DEEPSEEK_API_KEY`, `OPENROUTER_API_KEY`. If none are present, run `yp setup`.
3. **Azure CLI is logged in** (only for `azure-devops` subcommands). `az account show` must succeed; `yp` will install the `azure-devops` extension and prompt for login if not.

Skip the checks the user has clearly already passed (e.g. they just successfully ran `yp commit` two messages ago).

## Git workflows

### Commit and push

Default behavior: stage all changes, generate an AI commit message, copy it to clipboard, commit, and push.

```bash
yp commit                       # auto, no review
yp commit --confirm             # review the AI message before committing
yp commit --detailed            # subject + body, conventional commits
yp commit -l spanish            # write the message in Spanish
yp commit --save                # also save to commit-message-<timestamp>.md
```

Flag reference: `--confirm`, `--detailed`, `--save`, `--language|--lang|-l <lang>` (default: english). Flags compose freely.

Use `--confirm` whenever the user says "let me review" or anything cautious. Use `--detailed` when they ask for a "thorough", "explanatory" or "with body" commit, or when the diff is large enough that a single line clearly will not capture it.

### Pull request description

```bash
yp pr                           # interactive: pick source + target branches
yp pr --detailed                # adds summary, files changed, testing notes
yp pr --detailed -l french --save
```

`yp pr` always prompts the user to select branches interactively — there is currently no quick mode for branch selection. The output is copied to clipboard; `--save` writes to `pr-description-<from>-to-<to>.md`.

### Switch branches

```bash
yp checkout
```

Interactive branch selector with remote-tracking support. No flags. Use this when the user asks to "change branch", "switch to <branch>", or "checkout <branch>" without specifying — the menu lets them pick.

## Setup and skill installation

### Configure AI provider

```bash
yp setup
```

Walks the user through provider selection (OpenAI, Anthropic, Google Gemini, DeepSeek, OpenRouter), API key entry, model selection, and validation. Saves to `~/.yitpush/config.json`. After saving, optionally adds a `yitpush` shell alias and offers to install this skill.

### Install the yp agent skill

```bash
yp skill
```

Runs `npx skills add elvisbrevi/yitpush` so any Agent-Skills-compatible agent (Claude Code, Cursor, Gemini CLI…) gains this skill. Requires Node.js for `npx`. Use this when the user asks to "install the skill" or "let my agent use yp".

## Azure DevOps

All Azure DevOps subcommands live under `yp azure-devops <resource> <action>`. Most accept a "quick mode" with positional args that skip interactive menus — prefer it when the user has given org/project/id, since menus block the agent waiting for keystrokes.

Convention: `<org>` is the Azure DevOps organization name (the path segment after `dev.azure.com/`); `<project>` is the project name; IDs are numeric work-item IDs.

### User stories (HU)

```bash
yp azure-devops hu show <org> <hu-id>                                  # details + links
yp azure-devops hu list <org> <project> <hu-id>                        # list child tasks
yp azure-devops hu task <org> <project> <hu-id> [--description|-d "..."] [--effort|-e "4"]
yp azure-devops hu link <org> <project> <hu-id> --repo <repo> --branch <branch>
```

`hu task` without positional args opens an interactive HU picker; the picker also offers extra interactive-only actions ("Create branch for this HU" — generates `feature/<id>-<title>` — and "Mark as In Progress").

### Tasks

```bash
yp azure-devops task show <org> <task-id>
yp azure-devops task update <org> <task-id> [flags]
```

`update` flags (all optional, combine freely):

| Flag | Short | Purpose |
|------|-------|---------|
| `--effort` | `-e` | Estimated effort in hours (`Custom.EsfuerzoEstimadoHH`) |
| `--effort-real` | `-er` | Real effort spent (`Custom.EsfuerzoRealHH` + `CompletedWork`) |
| `--remaining` | `-r` | Remaining work in hours |
| `--state` | `-s` | New state — accepts `To Do`, `Doing`, `Active`, `In Progress`, `Resolved`, `Done`, `Closed`, `Removed` |
| `--comment` | `-c` | Append a discussion comment |

`task update` is also reachable as `hu update` and `wi update` — use whichever matches the work-item type the user named, but the underlying behavior is identical.

### Repositories and variable groups

```bash
yp azure-devops repo new                       # interactive create
yp azure-devops repo checkout                  # interactive clone
yp azure-devops variable-group list            # browse and inspect VGs
```

### Generic work-item link

```bash
yp azure-devops link <org> <project> <work-item-id>
```

Adds a branch/commit/PR link to any work item via `ArtifactLink`, so it appears under "Development" in the Azure Boards UI. Without args it falls back to interactive menus.

### Pure interactive entry point

```bash
yp azure-devops
```

Opens a top-level menu (Create repo, Clone repo, Browse variable groups, Create tasks for HU, List tasks of HU, Add link). Reach for this only when the user is exploring and hasn't given enough context for quick mode.

## Decision guide

Map intent → command. If a row matches the user's request, run that command. Substitute `<org>`, `<project>`, `<id>` with values from the user; ask only for missing pieces.

| User intent | Command |
|---|---|
| Commit and push | `yp commit` |
| Commit but let me review the message | `yp commit --confirm` |
| Detailed/long commit message | `yp commit --detailed` |
| Commit message in another language | `yp commit -l <language>` |
| Save the commit message to a file too | `yp commit --save` |
| Generate a PR description | `yp pr` |
| Detailed PR description with testing notes | `yp pr --detailed` |
| Switch / change git branch | `yp checkout` |
| Configure or change AI provider | `yp setup` |
| Install the yp skill in my agent | `yp skill` |
| Show user story <id> | `yp azure-devops hu show <org> <id>` |
| List tasks of user story <id> | `yp azure-devops hu list <org> <project> <id>` |
| Create tasks for user story <id> | `yp azure-devops hu task <org> <project> <id> [-d "..."] [-e "..."]` |
| Link branch to user story <id> | `yp azure-devops hu link <org> <project> <id> --repo <r> --branch <b>` |
| Show task <id> | `yp azure-devops task show <org> <id>` |
| Update task state | `yp azure-devops task update <org> <id> --state "Doing"` |
| Update task effort/remaining | `yp azure-devops task update <org> <id> --effort "8" --remaining "2"` |
| Add a comment to a task | `yp azure-devops task update <org> <id> --comment "<text>"` |
| Link a branch/commit/PR to any work item | `yp azure-devops link <org> <project> <id>` |
| Create a new Azure DevOps repo | `yp azure-devops repo new` |
| Clone an Azure DevOps repo | `yp azure-devops repo checkout` |
| List variable groups | `yp azure-devops variable-group list` |
| Browse Azure DevOps interactively | `yp azure-devops` |

## Operational notes

- All interactive menus include a `← Back` option; lists (HUs, tasks, projects, repos) sort by ID descending so the most recent items appear first.
- `yp` performs a daily background NuGet check and prints an upgrade hint when a newer version is available — non-fatal, ignore in automation.
- Error exit code is `1`; success is `0`. Respect this when chaining `yp` calls.
- `yp` writes commit messages and PR descriptions to the system clipboard. If the agent runs in a headless environment, prefer `--save` to also get a file copy.
