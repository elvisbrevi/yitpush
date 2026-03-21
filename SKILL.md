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

### tool: azure-devops

Manages Azure DevOps resources. Run without arguments for interactive menu, or pass subcommands directly.

```
yp azure-devops [subcommand] [args] [flags]
```

**Key subcommands**:

| Subcommand | Purpose |
|-----------|---------|
| `hu show <org> <id>` | Show User Story details |
| `hu list <org> <proj> <id>` | List tasks of a User Story |
| `hu task <org> <proj> <id>` | Create tasks for a User Story |
| `hu link <org> <proj> <id> --repo <r> --branch <b>` | Link a branch to a User Story |
| `task show <org> <id>` | Show task details |
| `task update <org> <id> [flags]` | Update task fields |
| `link <org> <proj> <id>` | Add a link to any work item |

**task update flags**: `--effort`, `--effort-real`, `--remaining`, `--state`, `--comment`

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

## Notes

- `yp` requires a configured AI provider (`yp setup`) or the `DEEPSEEK_API_KEY` env var
- All interactive menus support `← Back` navigation
- Version updates are shown automatically when a new version is available on NuGet
