# Publishing YitPush to NuGet.org

## Prerequisites

1. **NuGet.org account** – Sign up at https://www.nuget.org/
2. **API key** – Generate at https://www.nuget.org/account/apikeys with **Push** and **Push new packages** scopes
3. **.NET 10 SDK** – Installed on your machine

## Quick Publish (One‑Line)

```bash
dotnet clean && dotnet pack -c Release && dotnet nuget push bin/Release/YitPush.*.nupkg --api-key YOUR_API_KEY --source https://api.nuget.org/v3/index.json --skip-duplicate
```

## Step‑by‑Step

### 1. Bump the version in `YitPush.csproj`

```xml
<Version>1.3.1</Version>
```

Follow **Semantic Versioning**:

| Bump | When |
|------|------|
| `PATCH` (1.3.x) | Bug fixes |
| `MINOR` (1.x.0) | New features, backwards compatible |
| `MAJOR` (x.0.0) | Breaking changes |

Also update `<PackageReleaseNotes>` with a brief description of what changed.

### 2. Commit the version bump

```bash
git add YitPush.csproj
git commit -m "chore: bump version to 1.3.1"
git push
```

### 3. Clean, pack and publish

```bash
dotnet clean
dotnet pack -c Release
dotnet nuget push bin/Release/YitPush.1.3.1.nupkg \
  --api-key YOUR_API_KEY \
  --source https://api.nuget.org/v3/index.json
```

### 4. Verify

- Package available in **1–5 minutes** at https://www.nuget.org/packages/YitPush
- You'll receive a confirmation email from NuGet

## Troubleshooting

| Error | Solution |
|-------|----------|
| **Package already exists** | Increment the version in `.csproj` |
| **Invalid API key** | Verify the key has **Push** permissions |
| **Validation failed** | Check the confirmation email from NuGet for details |
| **Command not found** | Run from the project root directory |

## Useful Links

- [NuGet.org](https://www.nuget.org/)
- [API Keys](https://www.nuget.org/account/apikeys)
- [YitPush on NuGet](https://www.nuget.org/packages/YitPush)
