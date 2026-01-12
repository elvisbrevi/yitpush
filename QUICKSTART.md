# YitPush - Quick Start Guide

## What is YitPush?

YitPush is an AI-powered git commit tool that automates your entire commit workflow. Just run one command and let DeepSeek AI analyze your changes, generate a meaningful commit message, and push everything for you.

## Quick Installation

### 1. Install the Tool

**Linux/macOS:**
```bash
cd YitPush
./install.sh
```

**Windows:**
```powershell
cd YitPush
.\install.ps1
```

### 2. Set Your API Key

Get your DeepSeek API key from https://platform.deepseek.com/

**Linux/macOS:**
```bash
export DEEPSEEK_API_KEY='your-api-key-here'
echo 'export DEEPSEEK_API_KEY="your-api-key-here"' >> ~/.bashrc
```

**Windows (PowerShell as Admin):**
```powershell
[System.Environment]::SetEnvironmentVariable('DEEPSEEK_API_KEY', 'your-api-key-here', 'User')
```

### 3. Use It!

Navigate to any git repository and run:
```bash
yitpush
```

That's it! YitPush will:
- ✅ Analyze your changes
- ✅ Generate a smart commit message
- ✅ Ask for your confirmation
- ✅ Commit and push everything

## Alternative: Run Without Installing

If you prefer not to install the tool globally:

```bash
cd YitPush
dotnet run
```

This works like `npx` - you can run it directly without installation.

## Example Usage

```
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

## Troubleshooting

**"DEEPSEEK_API_KEY not found"**
- Make sure you've set the environment variable
- Restart your terminal after setting it

**"Not a git repository"**
- Run the command from inside a git repository

**"git push failed"**
- Make sure you have a remote repository configured
- Check your git credentials and SSH keys

## More Information

See the full [README.md](YitPush/README.md) for detailed documentation.
