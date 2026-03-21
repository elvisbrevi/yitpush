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
Run the setup wizard to configure your preferred AI provider:
```bash
yp setup
```

The wizard will guide you through:
1. Selecting a provider: **OpenAI**, **Anthropic**, **Google Gemini**, **DeepSeek**, or **OpenRouter**
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
| `yp setup` | Configure your AI provider interactively |
| `yp skill` | Install the yp skill for your AI agent |
| `yp commit` | Stage, commit and push with an AI-generated message |
| `yp checkout` | Interactive branch checkout |
| `yp pr` | Generate a pull request description between two branches |

### Azure DevOps Commands
| Command | Description |
|---------|-------------|
| `yp azure-devops` | Enter interactive Azure DevOps menu |
| `yp repo new` | Create a new repository |
| `yp repo checkout` | Clone/Checkout a repository |
| `yp hu task` | Create tasks for a User Story |
| `yp hu list` | List tasks of a User Story and manage them |
| `yp hu show` | Show details of a User Story (title, effort, links) |
| `yp task show` | Show details of a Task (effort, remaining, description) |
| `yp task update` | Update task fields (effort, state, comments) |
| `yp hu link` | Link a repository branch to a User Story |

## 🚀 Detailed Features

### 🤖 Multi-Provider AI Support

`yp` supports five AI providers. Run `yp setup` to switch between them at any time.

| Provider | Models |
|----------|--------|
| OpenAI | gpt-4o, gpt-4o-mini, o1, o1-mini, ... |
| Anthropic | claude-opus-4-6, claude-sonnet-4-6, claude-haiku-4-5 |
| Google Gemini | gemini-2.0-flash, gemini-1.5-pro, ... |
| DeepSeek | deepseek-chat, deepseek-reasoner |
| OpenRouter | 100+ models (google/gemini-2.0-flash, openai/gpt-4o, ...) |

### 📝 Smart Commits
`yp commit` analyzes your staged changes and generates a professional commit message.

```bash
yp commit                          # Auto commit and push
yp commit --confirm                # Review before committing
yp commit --detailed               # Generate title + body
yp commit --language spanish       # Output in Spanish
yp commit -l french                # Short flag for language
```

| Flag | Description |
|------|-------------|
| `--confirm` | Ask for confirmation before committing |
| `--detailed` | Generate detailed commit with title + body |
| `--language <lang>`, `--lang`, `-l` | Output language (default: english) |
| `--save` | Save commit message to a markdown file |

### 📋 Pull Request Descriptions
`yp pr` interactively selects two branches and generates a ready-to-paste PR description.

```bash
yp pr                              # Generate PR description
yp pr --detailed                   # Detailed with testing notes
yp pr --language spanish           # Output in Spanish
yp pr -l french                    # Short flag for language
yp pr --save                       # Save to markdown file
```

| Flag | Description |
|------|-------------|
| `--detailed` | Generate detailed PR description |
| `--language <lang>`, `--lang`, `-l` | Output language (default: english) |
| `--save` | Save PR description to a markdown file |

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

# Add a comment
yp azure-devops task update <org> <id> --comment "Progress update: logic refactored"

# Create tasks with info
yp azure-devops hu task <org> <proj> <hu-id> --description "Task info" --effort "4"
```

#### 🔗 Deep Linking
Link your local branch to an Azure DevOps work item natively:
```bash
yp azure-devops hu link <org> <proj> <id> --repo <name> --branch <name>
```
This uses `ArtifactLink`, making the branch appear in the **Development** section of the Azure Boards UI.

## 📝 Navigation
- Every interactive menu includes a **`← Back`** option.
- All lists (HUs, Tasks, Projects, Repos) are sorted by **Recency First** (ID Descending).
- **Auto-detection**: The tool automatically detects your organizations and projects.

---
*Created with ❤️ by Elvis Brevi*
