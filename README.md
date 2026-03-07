# 🚀 YitPush

AI-Powered Git Commit and Azure DevOps Management Tool.

## 🛠️ Installation

```bash
# Clone the repository
git clone https://github.com/elvisbrevi/yitpush.git
cd yitpush

# Build and install globally
dotnet pack -c Release
dotnet tool install --global --add-source ./nupkg yitpush
```

## 🔑 Configuration

Set your DeepSeek API key:
```bash
export DEEPSEEK_API_KEY='your-api-key-here'
```

## 📖 Usage

### Git Commands
| Command | Description |
|---------|-------------|
| `commit` | Stage, commit and push changes with an AI-generated message |
| `checkout` | Interactive branch checkout |
| `pr` | Generate a pull request description |

### Azure DevOps Commands
| Command | Description |
|---------|-------------|
| `azure-devops` | Enter interactive Azure DevOps menu |
| `repo new` | Create a new repository |
| `repo checkout`| Clone/Checkout a repository |
| `hu task` | Create tasks for a User Story |
| `hu list` | List tasks of a User Story and manage them |
| `hu show` | Show details of a User Story (title, effort, links) |
| `task show` | Show details of a Task (effort, remaining, description) |
| `task update`| Update task fields (effort, state, comments) |
| `hu link` | Link a repository branch to a User Story |

## 🚀 Detailed Features

### 📝 Smart Commits
`yitpush commit` analyzes your staged changes and generates a professional commit message using DeepSeek AI.
- `--confirm`: Review the message before committing.
- `--detailed`: Generate title and body.
- `--language <lang>`: Output in your preferred language.

### 🔷 Azure DevOps Integration

#### 📋 Task Management
- **List & Update**: Use `yitpush azure-devops hu list` to see all tasks of a HU. Select a task to:
    - View full **Description**.
    - See **Effort (HH)**, **Esfuerzo Real (HH)** and **Remaining Work**.
    - Inspect **Links** (Branches, Commits, PRs).
    - **Update** fields interactively.

#### ⚡ Quick Mode (CLI)
Update work items directly from your terminal:
```bash
# Update multiple fields
yitpush azure-devops task update <org> <id> --state "Active" --effort-real "5" --remaining "2"

# Add a comment
yitpush azure-devops task update <org> <id> --comment "Progress update: logic refactored"

# Create tasks with info
yitpush azure-devops hu task <org> <proj> <hu-id> --description "Task info" --effort "4"
```

#### 🔗 Deep Linking
Link your local branch to an Azure DevOps work item natively:
```bash
yitpush azure-devops hu link <org> <proj> <id> --repo <name> --branch <name>
```
This uses `ArtifactLink`, making the branch appear in the **Development** section of the Azure Boards UI.

## 📝 Navigation
- Every interactive menu includes a **`← Back`** option.
- All lists (HUs, Tasks, Projects, Repos) are sorted by **Recency First** (ID Descending).
- **Auto-detection**: The tool automatically detects your organizations and projects.

---
*Created with ❤️ by Elvis Brevi*
