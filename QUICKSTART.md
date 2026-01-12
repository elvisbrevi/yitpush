# YitPush - Quick Start Guide

Get up and running with YitPush in under 2 minutes.

## 1. Install the Tool

### Option A: Install from NuGet (Recommended)

```bash
dotnet tool install --global YitPush
```

### Option B: Run from Source

```bash
git clone https://github.com/elvisbrevi/yitpush.git
cd yitpush
dotnet run --project YitPush/YitPush.csproj
```

## 2. Set Your API Key

Get your DeepSeek API key from https://platform.deepseek.com/

**Linux/macOS:**
```bash
export DEEPSEEK_API_KEY='your-api-key-here'
echo 'export DEEPSEEK_API_KEY="your-api-key-here"' >> ~/.bashrc
```

**Windows (PowerShell):**
```powershell
$env:DEEPSEEK_API_KEY='your-api-key-here'
[System.Environment]::SetEnvironmentVariable('DEEPSEEK_API_KEY', 'your-api-key-here', 'User')
```

## 3. Use It!

Navigate to any git repository and run:

```bash
yitpush
```

That's it! YitPush will:
- ✅ Analyze your changes
- ✅ Generate a smart commit message
- ✅ Automatically commit and push (use `--confirm` to review first)
- ✅ Show success confirmation

## Example

```
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

See the full [README.md](README.md) for detailed documentation.