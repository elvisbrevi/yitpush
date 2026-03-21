# yp (YitPush) — Quick Start

Get up and running in under 2 minutes.

## 1. Install

```bash
dotnet tool install -g YitPush
```

## 2. Configure your AI provider

```bash
yp setup
```

The wizard will ask you to pick a provider (OpenAI, Anthropic, Google Gemini, DeepSeek, OpenRouter), enter your API key, and select a model. Config is saved to `~/.yitpush/config.json`.

> **Backward compatible**: if you already have `DEEPSEEK_API_KEY` set in your environment, `yp` will use it automatically without needing `yp setup`.

## 3. Use it!

Navigate to any git repository:

```bash
yp commit                   # auto commit and push
yp commit --confirm         # review message before committing
yp commit --detailed        # commit with title + body
yp commit -l spanish        # commit message in Spanish
yp pr                       # generate PR description
yp pr --detailed -l french  # detailed PR in French
yp checkout                 # switch branch interactively
```

## Azure DevOps

```bash
yp azure-devops hu show MyOrg 12345
yp azure-devops hu list MyOrg MyProj 12345
yp azure-devops task update MyOrg 67890 --state "Doing" --effort-real "3"
yp azure-devops hu link MyOrg MyProj 12345 --repo MyRepo --branch feature/abc
```

## Troubleshooting

| Problem | Solution |
|---------|----------|
| No AI provider configured | Run `yp setup` |
| Not a git repository | Run from inside a git repo |
| API key invalid | Re-run `yp setup` |
| git push failed | Check remote and credentials |

## More

- Full docs: `README.md`
- All commands and flags: `yp --help`
- AI agent docs: `llms.txt` / `llms-full.txt`
