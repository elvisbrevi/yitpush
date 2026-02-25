# 🚀 YitPush - AI-Powered Git Commit Tool

**Automate your entire git workflow with one command.**
YitPush uses DeepSeek's reasoning AI to analyze your changes, generate meaningful commit messages, and push everything automatically.

```bash
yitpush commit
yitpush commit --confirm
```

## ✨ Why YitPush?

- **🤖 AI-Powered**: Uses DeepSeek's reasoning model to generate contextual, conventional commit messages
- **🌐 Multi-Language**: Generate commit messages in any language with `--language`
- **📋 Detailed Commits**: Title + body + bullet points with `--detailed`
- **⚡ One-Command Workflow**: Replaces `git add . && git commit -m "..." && git push`
- **🔀 Branch Checkout**: Interactive branch switcher with `yitpush checkout`
- **📝 PR Descriptions**: AI-generated pull request descriptions with `yitpush pr`
- **☁️ Azure DevOps**: Create/clone repos and browse variable groups
- **🪟 Windows Support**: Full compatibility — detects `az.cmd` automatically on Windows
- **🌍 Cross-Platform**: macOS, Linux, and Windows with full Unicode/emoji support

## 📦 Installation

```bash
dotnet tool install --global YitPush
```

### Uninstall

```bash
dotnet tool uninstall --global YitPush
```

## 🔧 Configuration

### 1. Get Your DeepSeek API Key

1. Sign up at [DeepSeek Platform](https://platform.deepseek.com/)
2. Navigate to API Keys and create a new key

### 2. Set Environment Variable

**Linux/macOS:**
```bash
export DEEPSEEK_API_KEY='your-api-key-here'
# Make permanent
echo 'export DEEPSEEK_API_KEY="your-api-key-here"' >> ~/.zshrc
```

**Windows (PowerShell):**
```powershell
$env:DEEPSEEK_API_KEY='your-api-key-here'
# Make permanent (run as Admin)
[System.Environment]::SetEnvironmentVariable('DEEPSEEK_API_KEY', 'your-api-key-here', 'User')
```

## 🎯 Usage

Running `yitpush` with no arguments (or `--help`) shows the help screen:

```
Usage: yitpush <command> [options]
```

### Commands

| Command | Description |
|---------|-------------|
| `commit` | Stage, commit and push changes with an AI-generated message |
| `checkout` | Interactive branch checkout |
| `pr` | Generate a pull request description between two branches |
| `azure-devops` | Manage Azure DevOps resources (repos, variable groups) |

---

### `yitpush commit`

Stage, commit and push all changes using an AI-generated conventional commit message.

**Options:**

| Flag | Description |
|------|-------------|
| `--confirm` | Ask for confirmation before committing |
| `--detailed` | Generate detailed commit with title + body |
| `--language <lang>` | Output language (e.g., english, spanish, french) |
| `--save` | Save commit message to a markdown file |

**Examples:**
```bash
# Auto commit and push
yitpush commit

# Review before committing
yitpush commit --confirm

# Detailed commit message with body
yitpush commit --detailed

# Commit in Spanish
yitpush commit --language spanish

# Detailed + Spanish + confirmation
yitpush commit --detailed --lang es --confirm

# Save commit message to file
yitpush commit --save
```

---

### `yitpush checkout`

Interactive branch checkout. Lists all local and remote branches sorted by last commit date.

```bash
yitpush checkout
```

- Fetches remote branches automatically
- Shows branch type (local/remote) and last modified date
- Use `← Back` to cancel

---

### `yitpush pr`

Generate an AI-powered pull request description by comparing two branches.

**Options:**

| Flag | Description |
|------|-------------|
| `--detailed` | Generate detailed PR description (summary, changes, files, testing notes) |
| `--language <lang>` | Output language |
| `--save` | Save PR description to a markdown file |

**Examples:**
```bash
# Generate PR description (interactive branch selection)
yitpush pr

# Detailed PR description in Spanish
yitpush pr --detailed --lang es

# Generate and save to markdown file
yitpush pr --save

# All options combined
yitpush pr --detailed --language french --save
```

---

### `yitpush azure-devops`

Manage Azure DevOps resources. Requires [Azure CLI](https://docs.microsoft.com/en-us/cli/azure/install-azure-cli) installed and logged in (`az login`).

| Subcommand | Description |
|------------|-------------|
| `repo new` | Create a new repository interactively |
| `repo checkout` | Clone a repository interactively |
| `variable-group list` | List and inspect variable groups |
| `hu list` | List your User Stories and manage them (create tasks, branches) |

**Examples:**
```bash
# Create a new repo
yitpush azure-devops repo new

# Clone an existing repo
yitpush azure-devops repo checkout

# Browse variable groups
yitpush azure-devops variable-group list

# Browse and manage your User Stories
yitpush azure-devops hu list
```

**Features:**
- Auto-detects Azure DevOps organizations; falls back to manual entry if needed
- Installs the `azure-devops` CLI extension automatically if missing
- Smart defaults: suggests current directory name as repo name
- Handles existing remotes (choose between `azure`, `origin`, or custom)
- Full `← Back` navigation at every step
- **Windows**: automatically uses `cmd.exe /c az` to find `az.cmd`
- **User Stories**: Lists your assigned HUs by date, allowing you to create standard tasks, branches, or update status.

---

## 📝 Example Workflow

```bash
$ yitpush commit
🚀 YitPush - AI-Powered Git Commit Tool

📊 Analyzing git changes...
Found changes (1234 characters)

🤖 Generating commit message with DeepSeek Reasoning...

📝 Generated commit message:
   "feat: add user authentication with JWT tokens"

⏩ Proceeding automatically (use --confirm to review)...

⚙️  Executing git commands...
   git add .
   git commit -m "feat: add user authentication with JWT tokens"
   git push

✅ Successfully committed and pushed changes!
```

## ❓ Troubleshooting

| Problem | Solution |
|---------|----------|
| `❌ DEEPSEEK_API_KEY not found` | Set the environment variable as shown above |
| `❌ Not a git repository` | Run from inside a git repository |
| `❌ git push failed` | Check remote configuration and credentials |
| `❌ Failed to generate commit message` | Verify API key and internet connection |
| `❌ Azure CLI (az) is not installed` | Install [Azure CLI](https://docs.microsoft.com/en-us/cli/azure/install-azure-cli) |
| `⚠️  No organizations found` | Enter your organization name manually when prompted |
| `❌ No repositories found` | Verify the selected project contains repositories |

## 📚 Documentation

- **[Quick Start Guide](QUICKSTART.md)** – Get up and running in 2 minutes
- **[Publishing Guide](PUBLISH.md)** – How to publish new versions to NuGet
- **[DeepSeek API Docs](https://api-docs.deepseek.com/)** – About the AI model

## 📄 License

MIT License – see [LICENSE](LICENSE) file.

## 👨‍💻 Author

**Elvis Brevi** – [GitHub](https://github.com/elvisbrevi)

## 🤝 Contributing

Contributions are welcome! Please feel free to submit a Pull Request.

1. Fork the repository
2. Create your feature branch (`git checkout -b feature/amazing-feature`)
3. Commit your changes (`yitpush commit`)
4. Open a Pull Request

## ⭐ Support

If you find YitPush useful, please consider giving it a star on GitHub!
