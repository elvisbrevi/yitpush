---
name: yp
description: >
  Use this skill to automate git commits with AI-generated messages, generate pull request
  descriptions, and manage Azure DevOps work items. Invoke when the user asks to commit
  changes, create a PR description, switch branches, or manage Azure DevOps tasks and user
  stories. Requires the yp CLI tool (dotnet global tool).
license: MIT
compatibility: >
  Requires .NET 10 SDK or Runtime and the yp global tool installed
  (dotnet tool install -g YitPush). Works on macOS, Linux, and Windows.
  Requires git. Azure DevOps features require the Azure CLI (az).
metadata:
  author: Elvis Brevi
  version: "2.0.0"
  repository: https://github.com/elvisbrevi/yitpush
  install: dotnet tool install -g YitPush
  update: dotnet tool update -g YitPush
---

# yp — AI-Powered Git & Azure DevOps CLI

`yp` (YitPush) is a CLI tool that generates commit messages and PR descriptions using
configurable AI providers (OpenAI, Anthropic, Google Gemini, DeepSeek, OpenRouter),
and manages Azure DevOps work items.

## Prerequisites

Check if `yp` is installed:
```bash
yp --help
```

If not installed:
```bash
dotnet tool install -g YitPush
```

Check if a provider is configured:
```bash
cat ~/.yitpush/config.json
```

If not configured (or file doesn't exist), run setup:
```bash
yp setup
```

## Commit changes

Generate an AI commit message, stage all changes, commit, and push:
```bash
yp commit
```

With confirmation prompt before committing:
```bash
yp commit --confirm
```

Detailed commit (subject + body):
```bash
yp commit --detailed
```

Commit message in a specific language:
```bash
yp commit -l spanish
yp commit --language french
```

Save commit message to file:
```bash
yp commit --save
```

## Generate PR description

Select source and target branches interactively, generate a description, and copy to clipboard:
```bash
yp pr
```

Detailed PR with summary, changes, files, and testing notes:
```bash
yp pr --detailed
```

PR description in a specific language:
```bash
yp pr -l spanish
yp pr --detailed -l french --save
```

## Switch branches

Interactive branch selector:
```bash
yp checkout
```

## Configure AI provider

First-time setup or to change provider/model/key:
```bash
yp setup
```

Supported providers: OpenAI, Anthropic, Google Gemini, DeepSeek, OpenRouter.
Config is saved to `~/.yitpush/config.json`.

## Install the agent skill

Install the `yp` skill so any Agent Skills-compatible agent (Claude Code, Cursor, Gemini CLI, etc.) knows how to invoke `yp`:
```bash
yp skill
```

Internally runs `npx skills add elvisbrevi/yitpush`. Requires Node.js (`npx`).

## Azure DevOps — User Stories

Show User Story details:
```bash
yp azure-devops hu show <org> <hu-id>
```

List all tasks of a User Story:
```bash
yp azure-devops hu list <org> <project> <hu-id>
```

Create tasks for a User Story (interactive):
```bash
yp azure-devops hu task
```

Create tasks directly:
```bash
yp azure-devops hu task <org> <project> <hu-id> --description "Backend work" --effort "4"
```

Link a branch to a User Story:
```bash
yp azure-devops hu link <org> <project> <hu-id> --repo <repo-name> --branch <branch-name>
```

## Azure DevOps — Tasks

Show task details:
```bash
yp azure-devops task show <org> <task-id>
```

Update task fields:
```bash
yp azure-devops task update <org> <task-id> --state "Doing"
yp azure-devops task update <org> <task-id> --effort "8" --effort-real "3" --remaining "5"
yp azure-devops task update <org> <task-id> --comment "Progress update"
```

Available `--state` values: `To Do`, `Doing`, `Done`, `Active`, `Resolved`, `Closed`.

## Azure DevOps — Links & Repos

Add a branch/commit/PR link to any work item:
```bash
yp azure-devops link <org> <project> <work-item-id>
```

Create a new repository:
```bash
yp azure-devops repo new
```

Clone a repository interactively:
```bash
yp azure-devops repo checkout
```

List and inspect variable groups:
```bash
yp azure-devops variable-group list
```

## Decision Guide

| User asks to... | Command |
|----------------|---------|
| Commit my changes | `yp commit` |
| Commit and let me review first | `yp commit --confirm` |
| Write a detailed commit | `yp commit --detailed` |
| Generate a PR description | `yp pr` |
| Switch branches | `yp checkout` |
| Set up a new AI provider | `yp setup` |
| Install the yp agent skill | `yp skill` |
| List variable groups | `yp azure-devops variable-group list` |
| Create a new repo | `yp azure-devops repo new` |
| Clone a repo | `yp azure-devops repo checkout` |
| Show user story details | `yp azure-devops hu show <org> <id>` |
| List tasks of a user story | `yp azure-devops hu list <org> <proj> <id>` |
| Update a task's state | `yp azure-devops task update <org> <id> --state "..."` |
| Add a comment to a task | `yp azure-devops task update <org> <id> --comment "..."` |
| Link a branch to a user story | `yp azure-devops hu link <org> <proj> <id> --repo <r> --branch <b>` |
