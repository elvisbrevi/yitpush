# YitPush

AI-Powered Git Commit Tool using DeepSeek Reasoning

YitPush automates your git workflow by analyzing your code changes with DeepSeek's reasoning AI and generating meaningful commit messages.

## Features

- Automatically analyzes git diff
- Generates conventional commit messages using DeepSeek Reasoning AI
- Executes git add, commit, and push with one command
- User confirmation before committing
- Beautiful CLI output with colors

## Prerequisites

- .NET 10.0 or later
- Git
- DeepSeek API key

## Installation

### Option 1: Using Installation Script (Recommended)

**Linux/macOS:**
```bash
cd YitPush
./install.sh
```

**Windows (PowerShell):**
```powershell
cd YitPush
.\install.ps1
```

### Option 2: Manual Installation

```bash
# Build and pack the tool
cd YitPush
dotnet pack -c Release

# Install globally
dotnet tool install --global --add-source ./bin/Release YitPush --version 1.0.0

# Now you can use it from anywhere
yitpush
```

### Option 3: Run without Installation (Using dotnet run)

```bash
# Clone the repository
cd YitPush

# Run directly (similar to npx)
dotnet run
```

## Setup

Set your DeepSeek API key as an environment variable:

```bash
# Linux/macOS
export DEEPSEEK_API_KEY='your-api-key-here'

# Windows PowerShell
$env:DEEPSEEK_API_KEY='your-api-key-here'

# Windows Command Prompt
set DEEPSEEK_API_KEY=your-api-key-here
```

To make it permanent:

```bash
# Linux/macOS (add to ~/.bashrc or ~/.zshrc)
echo 'export DEEPSEEK_API_KEY="your-api-key-here"' >> ~/.bashrc
source ~/.bashrc

# Windows (PowerShell as Administrator)
[System.Environment]::SetEnvironmentVariable('DEEPSEEK_API_KEY', 'your-api-key-here', 'User')
```

Get your DeepSeek API key from: https://platform.deepseek.com/

## Usage

Navigate to your git repository and run:

```bash
# If installed globally
yitpush

# If running without installation
dotnet run
```

The tool will:
1. Check if you're in a git repository
2. Analyze your changes with `git diff`
3. Generate a commit message using DeepSeek Reasoning AI
4. Ask for confirmation
5. Execute `git add .`, `git commit`, and `git push`

## Example

```bash
$ yitpush
🚀 YitPush - AI-Powered Git Commit Tool

📊 Analyzing git changes...
Found changes (1234 characters)

🤖 Generating commit message with DeepSeek Reasoning...

📝 Generated commit message:
   "feat: add user authentication with JWT tokens"

Do you want to proceed with this commit? (y/n): y

⚙️  Executing git commands...
   git add .
   git commit -m "feat: add user authentication with JWT tokens"
   git push

✅ Successfully committed and pushed changes!
```

## Uninstall

```bash
dotnet tool uninstall -g YitPush
```

## How It Works

YitPush uses the DeepSeek Reasoning model (`deepseek-reasoner`) which:
- Analyzes your code changes with chain-of-thought reasoning
- Generates contextual commit messages following conventional commits format
- Ensures high-quality, descriptive commit messages

## Troubleshooting

### API Key Not Found
Make sure you've set the `DEEPSEEK_API_KEY` environment variable and restarted your terminal.

### Not a Git Repository
Run the command from within a git repository directory.

### No Changes Detected
Make sure you have uncommitted changes in your repository.

### Git Push Failed
Ensure you have:
- A remote repository configured
- Proper authentication (SSH keys or credentials)
- The correct upstream branch set

## License

MIT

## Author

Elvis Brevi

## Contributing

Contributions are welcome! Please feel free to submit a Pull Request.
