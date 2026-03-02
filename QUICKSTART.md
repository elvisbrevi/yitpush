# YitPush - Quick Start Guide

Get up and running with YitPush in under 2 minutes.

## 1. Install the Tool

```bash
dotnet tool install --global YitPush
```

## 2. Set Your API Key

Get your DeepSeek API key from https://platform.deepseek.com/

**Linux/macOS:**
```bash
export DEEPSEEK_API_KEY='your-api-key-here'
echo 'export DEEPSEEK_API_KEY="your-api-key-here"' >> ~/.zshrc
```

**Windows (PowerShell):**
```powershell
$env:DEEPSEEK_API_KEY='your-api-key-here'
[System.Environment]::SetEnvironmentVariable('DEEPSEEK_API_KEY', 'your-api-key-here', 'User')
```

## 3. Use It!

Navigate to any git repository. Run `yitpush` to see all available commands:

```
Usage: yitpush <command> [options]
```

### Commit and push your changes

```bash
yitpush commit
```

YitPush will:
- ✅ Analyze your staged, unstaged and untracked changes
- ✅ Generate a smart conventional commit message with DeepSeek AI
- ✅ Run `git add .`, `git commit` and `git push` automatically
- ✅ Copy the commit message to your clipboard

### Review before committing

```bash
yitpush commit --confirm
```

### Switch branches interactively

```bash
yitpush checkout
```

### Generate a PR description

```bash
yitpush pr
```

## Example

```
$ yitpush commit
🚀 YitPush - AI-Powered Git Commit Tool

📊 Analyzing git changes...
Found changes (1234 characters)

🤖 Generating commit message with DeepSeek...

📝 Generated commit message:
   "feat: add user authentication with JWT tokens"

⏩ Proceeding automatically (use --confirm to review)...

⚙️  Executing git commands...
   git add .
   git commit -m "feat: add user authentication with JWT tokens"
   git push

✅ Successfully committed and pushed changes!
```

## Useful Options

| Command | Description |
|---------|-------------|
| `yitpush commit --confirm` | Review commit message before proceeding |
| `yitpush commit --detailed` | Generate commit with title + body |
| `yitpush commit --lang es` | Commit message in Spanish |
| `yitpush commit --save` | Save commit message to a markdown file |
| `yitpush checkout` | Interactive branch switcher |
| `yitpush pr` | Generate AI pull request description |
| `yitpush pr --detailed --save` | Detailed PR description, saved to file |
| `yitpush azure-devops repo new` | Create an Azure DevOps repository |
| `yitpush azure-devops repo checkout` | Clone an Azure DevOps repository |
| `yitpush azure-devops hu list` | List and manage your User Stories |

## Troubleshooting

**"DEEPSEEK_API_KEY not found"**
- Make sure you've set the environment variable and restarted your terminal

**"Not a git repository"**
- Run the command from inside a git repository

**"git push failed"**
- Make sure you have a remote configured and valid credentials

## More Information

See the full [README.md](README.md) for detailed documentation.
