# 🚀 YitPush - AI-Powered Git Commit Tool

**Automate your entire git workflow with one command.**  
YitPush uses DeepSeek's reasoning AI to analyze your changes, generate meaningful commit messages, and push everything automatically.

```bash
# Just run this
yitpush

# Or with confirmation
yitpush --confirm
```

## ✨ Why YitPush?

- **🤖 AI-Powered**: Uses DeepSeek's reasoning model to generate contextual, conventional commit messages
- **🌐 Multi-Language Support**: Generate commit messages in multiple languages with `--language` flag (English, Spanish, French, etc.)
- **📋 Detailed Commits**: Generate comprehensive commit messages with body and bullet points using `--detailed` flag
- **⚡ One-Command Workflow**: Replaces `git add . && git commit -m "..." && git push` with a single command
- **🚀 Fast & Efficient**: Automatically proceeds without confirmation (use `--confirm` to review)
- **🔒 Safe & Reliable**: Full git diff analysis and error handling
- **📋 PR Descriptions**: Generate AI-powered pull request descriptions by comparing branches with `--pr-description`
- **💾 Save to File**: Optionally save commit messages or PR descriptions to markdown files with `--save`
- **☁️ Azure DevOps Integration**: Create/clone repos and browse variable groups with `azure-devops` subcommands
- **🔀 Back Navigation**: All interactive flows support `← Back` to return to the previous step
- **🌍 Cross-Platform Compatibility**: Install as a global .NET tool or run directly from source, with full Unicode/emoji support for Windows PowerShell, macOS Terminal, and Linux

## 📦 Installation

### Recommended: Install from NuGet (Global Tool)

```bash
dotnet tool install --global YitPush
```

### Alternative: Run from Source

```bash
# Clone the repository
git clone https://github.com/elvisbrevi/yitpush.git
cd yitpush

# Build and run directly
dotnet run --project YitPush/YitPush.csproj
```

### Uninstall

```bash
dotnet tool uninstall --global YitPush
```

## 🔧 Configuration

### 1. Get Your DeepSeek API Key

1. Sign up at [DeepSeek Platform](https://platform.deepseek.com/)
2. Navigate to API Keys section
3. Create a new API key

### 2. Set Environment Variable

**Linux/macOS (bash/zsh):**
```bash
export DEEPSEEK_API_KEY='your-api-key-here'
# Make permanent
echo 'export DEEPSEEK_API_KEY="your-api-key-here"' >> ~/.bashrc
```

**Windows (PowerShell):**
```powershell
$env:DEEPSEEK_API_KEY='your-api-key-here'
# Make permanent (run as Admin)
[System.Environment]::SetEnvironmentVariable('DEEPSEEK_API_KEY', 'your-api-key-here', 'User')
```

## 🎯 Usage

### Basic Usage

Navigate to any git repository and run:

```bash
yitpush
```

YitPush will:
1. 📊 Analyze your git changes (staged, unstaged, and untracked files)
2. 🤖 Generate a commit message using DeepSeek AI
3. ⚙️ Automatically execute `git add .`, `git commit`, and `git push`
4. ✅ Show success confirmation

Use the `--detailed` flag to generate a commit message with body including explanations and bullet points.

### Command Line Options

| Option | Short | Description |
|--------|-------|-------------|
| `--confirm` | | Ask for confirmation before committing (default: automatic) |
| `--detailed` | | Generate detailed commit with body (title + paragraphs + bullet points) |
| `--language` | `--lang` | Set output language for commit message (e.g., 'english', 'spanish', 'french') |
| `--check` | | Interactive branch checkout |
| `--pr-description` | | Generate a PR description by comparing two branches |
| `--save` | | Save the output to a markdown file |
| `--help` | `-h` | Show help message with all available options |

**Subcommands:**

| Command | Description |
|---------|-------------|
| `azure-devops repo new` | Create a new Azure DevOps repository interactively |
| `azure-devops repo checkout` | Clone an Azure DevOps repository interactively |
| `azure-devops variable-group list` | Browse and inspect variable groups in an Azure DevOps project |

**Examples:**
```bash
# Automatic commit and push (default)
yitpush

# Review commit message before proceeding
yitpush --confirm

# Generate detailed commit message with body
yitpush --detailed

# Generate commit message in Spanish
yitpush --language spanish

# Generate detailed commit message in French with confirmation
yitpush --language fr --detailed --confirm

# Use short flag for language
yitpush --lang es

# Combine all options: Spanish, detailed, with confirmation
yitpush --language spanish --detailed --confirm

# Create a new Azure DevOps repo
yitpush azure-devops repo new

# Clone an existing Azure DevOps repo interactively
yitpush azure-devops repo checkout

# List and inspect variable groups
yitpush azure-devops variable-group list

# Generate PR description (interactive branch selection)
yitpush --pr-description

# PR description in Spanish, detailed mode
yitpush --pr-description --detailed --lang es

# PR description and save to markdown file
yitpush --pr-description --save

# Save commit message to markdown file
yitpush --save

# Show help
yitpush --help
yitpush -h
```

### Detailed Mode

When using the `--detailed` flag, YitPush generates comprehensive commit messages with:

- **Title line**: Conventional commit type with concise subject (max 50 characters)
- **Body paragraphs**: 1-2 paragraphs explaining the changes and rationale
- **Bullet points**: Key changes listed with clear formatting
- **File mentions**: Important files modified or added are highlighted
- **Git compatibility**: All lines wrapped at 72 characters for optimal git log display

This is ideal for complex changes that require detailed documentation or team communication.

### Multi-language Support

YitPush now supports generating commit messages in multiple languages using the `--language` or `--lang` flag:

- **Supported languages**: English (default), Spanish, French, German, Italian, Portuguese, and more
- **Flexible syntax**: Use `--language spanish`, `--lang es`, `--language=fr`, or `--lang=it`
- **Combines with other flags**: Works with `--detailed` and `--confirm` flags
- **AI-powered translations**: DeepSeek AI generates culturally appropriate commit messages in the target language

**Examples:**
```bash
# Generate commit in Spanish
yitpush --language spanish

# Detailed commit in French with confirmation
yitpush --language fr --detailed --confirm

# Simple commit in Italian
yitpush --lang it
```

The language flag instructs the AI model to generate commit messages following conventional commit format but in the specified language, maintaining all formatting and style requirements.

### Azure DevOps Integration

YitPush provides Azure DevOps subcommands for repository and variable group management:

#### Create Repository

```bash
yitpush azure-devops repo new
```

- **Interactive setup**: Guides you through selecting organization, project, and repository name
- **Requires Azure CLI**: The `az` CLI must be installed with the `azure-devops` extension (auto-installed if missing)
- **Smart defaults**: Suggests the current directory name as the repository name
- **Handles existing repos**: Detects if the repository already exists and offers to reuse it
- **Flexible remotes**: If `origin` is already configured, lets you choose an alternative remote name (e.g., `azure`)
- **Back navigation**: Use `← Back` to return to previous steps at any point

#### Clone Repository

```bash
yitpush azure-devops repo checkout
```

- **Interactive selection**: Guides you through selecting organization, project, and repository from lists
- **Requires Azure CLI**: The `az` CLI must be installed with the `azure-devops` extension (auto-installed if missing)
- **Custom destination**: Prompts for the target directory with `<current_dir>/<repo_name>` as default
- **Back navigation**: Use `← Back` to return to previous steps at any point

#### Variable Groups

```bash
yitpush azure-devops variable-group list
```

- **Selectable list**: Browse variable groups with an interactive selection prompt
- **Variable inspection**: Select a group to view its variables in a formatted table
- **Secret handling**: Secret values are displayed as `******`
- **Back navigation**: Return to the list after inspecting a group, or exit with `← Back`

**Prerequisites:**
- [Azure CLI](https://docs.microsoft.com/en-us/cli/azure/install-azure-cli) installed
- Logged into Azure (`az login` - YitPush will prompt if not logged in)
- Access to an Azure DevOps organization and project

### PR Description Generator

YitPush can generate AI-powered pull request descriptions by comparing two branches using the `--pr-description` flag:

- **Interactive branch selection**: Pick source and target branches from a list showing local/remote type and last modified date
- **AI-generated markdown**: Produces a structured PR description with title, summary, and changes
- **Combinable flags**: Works with `--detailed`, `--language`, and `--save`
- **Back navigation**: Use `← Back` to return to source branch selection from target
- **Separate flow**: Does not commit or push — only generates the description

**Examples:**
```bash
# Generate PR description
yitpush --pr-description

# Detailed PR description in Spanish
yitpush --pr-description --detailed --lang es

# Generate and save to markdown file
yitpush --pr-description --save

# All options combined
yitpush --pr-description --detailed --language french --save
```

### Save to File

Use the `--save` flag to save the output to a markdown file. Works with both the default commit flow and the PR description mode:

- **Commit mode**: Saves the commit message to `commit-message-<timestamp>.md`
- **PR description mode**: Saves the PR description to `pr-description-<source>-to-<target>.md`
- **Disabled by default**: Output is only displayed on screen unless `--save` is specified

**Examples:**
```bash
# Save commit message to file
yitpush --save

# Save PR description to file
yitpush --pr-description --save
```

## 📝 Example Workflow

```bash
$ yitpush
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

## 🛠️ Advanced

### Running Without Installation

You can use YitPush without installing it globally:

```bash
# From the project root
dotnet run --project YitPush/YitPush.csproj

# Or navigate to the project directory
cd YitPush && dotnet run
```

### How It Works

1. **Git Diff Analysis**: Collects staged, unstaged, and untracked file changes
2. **AI Processing**: Sends diff to DeepSeek Reasoning API with optimized prompt
3. **Commit Generation**: Returns a concise, conventional commit message
4. **Git Execution**: Runs `git add .`, `git commit -m "..."`, and `git push`
5. **Error Handling**: Provides clear error messages for common issues

## 📚 Documentation

- **[Quick Start Guide](QUICKSTART.md)** – Get up and running in 2 minutes
- **[Publishing Guide](PUBLISH.md)** – How to publish new versions to NuGet
- **[DeepSeek API Docs](https://api-docs.deepseek.com/)** – About the AI model

## ❓ Troubleshooting

| Problem | Solution |
|---------|----------|
| `❌ Error: DEEPSEEK_API_KEY environment variable not found` | Set the environment variable as shown above |
| `❌ Error: Not a git repository` | Run the command from inside a git repository |
| `❌ Error: git push failed` | Check remote configuration and credentials |
| `❌ Error: Failed to generate commit message` | Verify API key and internet connection |
| `❌ Azure CLI (az) is not installed` | Install [Azure CLI](https://docs.microsoft.com/en-us/cli/azure/install-azure-cli) |
| `❌ No Azure DevOps organizations found` | Verify your Azure account has access to Azure DevOps |
| `❌ No repositories found in this project` | Verify the selected project contains repositories |
| `❌ Failed to clone repository` | Check network access and Azure DevOps permissions |

## 🏗️ Project Structure

```
yitpush/
├── YitPush/                 # Main project
│   ├── Program.cs           # Application logic
│   └── YitPush.csproj       # Project configuration
├── README.md                # This file
├── QUICKSTART.md            # Quick start guide
├── PUBLISH.md               # Concise publishing guide
└── LICENSE                  # MIT License
```

## 📄 License

MIT License – see [LICENSE](LICENSE) file.

## 👨‍💻 Author

**Elvis Brevi** – [GitHub](https://github.com/elvisbrevi)

## 🤝 Contributing

Contributions are welcome! Please feel free to submit a Pull Request.

1. Fork the repository
2. Create your feature branch (`git checkout -b feature/amazing-feature`)
3. Commit your changes (`git commit -m 'Add amazing feature'`)
4. Push to the branch (`git push origin feature/amazing-feature`)
5. Open a Pull Request

## ⭐ Support

If you find YitPush useful, please consider giving it a star on GitHub!