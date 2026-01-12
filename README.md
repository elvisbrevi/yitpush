# YitPush

**AI-Powered Git Commit Tool using DeepSeek Reasoning**

Automate your git workflow with AI. YitPush analyzes your code changes using DeepSeek's reasoning model and handles the entire commit-push process with one command.

```bash
# Just run this
yitpush

# Or without installation
dotnet run
```

## Why YitPush?

- **One Command**: Replace `git add . && git commit -m "..." && git push` with just `yitpush`
- **Smart Commits**: DeepSeek AI generates meaningful commit messages following conventional commits format
- **Safe**: Always asks for confirmation before committing
- **Fast**: Powered by DeepSeek's latest reasoning model
- **Easy**: Works like `npx` - can run without installation using `dotnet run`

## Quick Start

### Installation from NuGet (Recommended)

```bash
# Install globally from NuGet.org
dotnet tool install --global YitPush
```

### Or Install from Source

```bash
cd YitPush
./install.sh        # Linux/macOS
# or
.\install.ps1       # Windows
```

### Or Run Without Installing

**Opción más fácil (desde la raíz):**
```bash
./run.sh        # Linux/macOS
# o
.\run.ps1       # Windows
```

**Otras opciones:**
```bash
# Desde el directorio YitPush
cd YitPush && dotnet run

# Con dotnet run desde la raíz
dotnet run --project YitPush/YitPush.csproj
```

### Setup API Key

Get your DeepSeek API key from https://platform.deepseek.com/

```bash
# Linux/macOS
export DEEPSEEK_API_KEY='your-api-key-here'
echo 'export DEEPSEEK_API_KEY="your-key"' >> ~/.bashrc

# Windows PowerShell
[System.Environment]::SetEnvironmentVariable('DEEPSEEK_API_KEY', 'your-key', 'User')
```

### Use It!

```bash
yitpush
```

See [QUICKSTART.md](QUICKSTART.md) for detailed instructions.

## Features

- ✅ Analyzes git diff automatically
- ✅ Generates conventional commit messages with AI
- ✅ Executes git add, commit, and push in one go
- ✅ User confirmation before committing
- ✅ Beautiful CLI with colors and emojis
- ✅ Works as global tool or with `dotnet run`
- ✅ Built with .NET 10

## Documentation

- [Quick Start Guide](QUICKSTART.md) - Get up and running in 2 minutes
- [Full Documentation](YitPush/README.md) - Complete guide with all features
- [DeepSeek Reasoning Model](https://api-docs.deepseek.com/guides/reasoning_model) - About the AI model

## Project Structure

```
yitpush/
├── YitPush/              # Main project
│   ├── Program.cs        # Application logic
│   ├── YitPush.csproj    # Project configuration
│   ├── README.md         # Detailed documentation
│   ├── install.sh        # Linux/macOS installation
│   └── install.ps1       # Windows installation
├── QUICKSTART.md         # Quick start guide
└── README.md            # This file
```

## Requirements

- .NET 10.0 or later
- Git
- DeepSeek API key

## How It Works

1. Runs `git diff` to analyze your changes
2. Sends the diff to DeepSeek Reasoning API
3. AI generates a commit message using chain-of-thought reasoning
4. Shows you the message and asks for confirmation
5. Executes `git add .`, `git commit`, and `git push`

## Example

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

## Uninstall

```bash
dotnet tool uninstall -g YitPush
```

## License

MIT

## Author

Elvis Brevi

## Contributing

Contributions welcome! Feel free to open issues or submit pull requests.

## About DeepSeek

YitPush uses the `deepseek-reasoner` model, which employs chain-of-thought reasoning to generate high-quality, contextual commit messages. Learn more at the [DeepSeek API Documentation](https://api-docs.deepseek.com/).
