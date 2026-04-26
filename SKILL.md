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
yp commit [--confirm] [--detailed] [--language <lang>] [--save]
```

**When to use**: User wants to commit and push changes with an AI-generated message.

**Parameters**:
- `--confirm` — pause and ask user to approve the message before committing
- `--detailed` — generate a commit with a subject line + body paragraph
- `--language <lang>` / `-l <lang>` — language for the output (e.g., spanish, french, portuguese)
- `--save` — write the commit message to a `.md` file in the current directory

**Examples**:
```bash
yp commit
yp commit --confirm
yp commit --detailed -l spanish
```

---

### tool: pr

Interactively selects two branches, generates a pull request description and copies it to the clipboard.

```
yp pr [--detailed] [--language <lang>] [--save]
```

**When to use**: User wants to draft a PR description for a branch.

**Parameters**:
- `--detailed` — include summary, change list, files changed, and testing notes
- `--language <lang>` / `-l <lang>` — language for the output
- `--save` — save description to `pr-description-<from>-to-<to>.md`

**Examples**:
```bash
yp pr
yp pr --detailed
yp pr -l french --save
```

---

### tool: setup

Configures the active AI provider interactively.

```
yp setup
```

**When to use**: First-time setup or when changing the AI provider or API key.

**Flow**: select provider → enter API key → select model → validate → save to `~/.yitpush/config.json`

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
| `hu link <org> <proj> <id> --repo <r> --branch <b>` | Link a branch to a User Story |
| `task show <org> <id>` | Show task details |
| `task update <org> <id> [flags]` | Update task fields (alias: `hu update`, `wi update`) |
| `link <org> <proj> <id>` | Add a link (branch/commit/PR) to any work item |

**task update flags**: `--effort|-e`, `--effort-real|-er`, `--remaining|-r`, `--state|-s`, `--comment|-c`
**hu task flags**: `--description|-d`, `--effort|-e`
**hu link flags**: `--repo`, `--branch`

---

## Usage Instructions for Gemini CLI

When the user asks you to:

- **"commit my changes"** → run `yp commit`
- **"commit and let me review"** → run `yp commit --confirm`
- **"generate a PR description"** → run `yp pr`
- **"switch branch"** → run `yp checkout`
- **"show user story 12345"** → run `yp azure-devops hu show <org> 12345`
- **"update task 67890 state to Doing"** → run `yp azure-devops task update <org> 67890 --state "Doing"`
- **"configure AI provider"** → run `yp setup`
- **"install the yp skill"** → run `yp skill`
- **"list variable groups"** → run `yp azure-devops variable-group list`
- **"create a new repo"** → run `yp azure-devops repo new`
- **"clone a repo"** → run `yp azure-devops repo checkout`
- **"link a branch to work item 67890"** → run `yp azure-devops link <org> <proj> 67890`

## Notes

- `yp` requires a configured AI provider (`yp setup`) or the `DEEPSEEK_API_KEY` env var
- All interactive menus support `← Back` navigation
- Version updates are shown automatically when a new version is available on NuGet
