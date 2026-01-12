# Publishing YitPush to NuGet.org

This is the fastest, most concise guide to publish YitPush to NuGet.org. It uses manual commands only—no scripts required.

## Prerequisites

1. **NuGet.org account** – Sign up at https://www.nuget.org/
2. **API key** – Generate at https://www.nuget.org/account/apikeys with **Push** and **Push new packages** scopes
3. **.NET 10 SDK** – Installed on your machine

## Quick Publish (One‑Line)

Replace `YOUR_API_KEY` with your actual NuGet API key.

```bash
dotnet clean && dotnet pack -c Release && dotnet nuget push bin/Release/YitPush.*.nupkg --api-key YOUR_API_KEY --source https://api.nuget.org/v3/index.json --skip-duplicate
```

## Step‑by‑Step Manual Commands

### 1. Update Version (if needed)

Edit `YitPush.csproj` and change the `<Version>` element:

```xml
<Version>1.0.1</Version>
```

### 2. Clean and Pack

```bash
dotnet clean
dotnet pack -c Release
```

This creates a `.nupkg` file in `bin/Release/`.

### 3. Push to NuGet.org

```bash
dotnet nuget push bin/Release/YitPush.1.0.1.nupkg \
  --api-key YOUR_API_KEY \
  --source https://api.nuget.org/v3/index.json
```

Replace `YOUR_API_KEY` with your actual API key and adjust the version number.

### 4. Wait and Verify

- The package will be available in **1‑5 minutes**
- Check https://www.nuget.org/packages/YitPush
- You’ll receive a confirmation email

## Updating the Package

1. Increment the version in `YitPush.csproj`
2. Run the **Quick Publish** command again

Users can update with:

```bash
dotnet tool update --global YitPush
```

## Troubleshooting

| Error | Solution |
|-------|----------|
| **Package already exists** | Increase the version number in `.csproj` |
| **Invalid API key** | Verify the key has **Push** permissions |
| **Validation failed** | Check the email from NuGet for details |
| **Command not found** | Ensure you’re in the `YitPush` project directory |

## After Publishing

Users can install the tool globally:

```bash
dotnet tool install --global YitPush
```

And run it with:

```bash
yitpush
```

## Versioning

Follow **Semantic Versioning** (`MAJOR.MINOR.PATCH`):

- **PATCH** (`1.0.1`) – Bug fixes
- **MINOR** (`1.1.0`) – New features, backwards compatible
- **MAJOR** (`2.0.0`) – Breaking changes

## Useful Links

- [NuGet.org](https://www.nuget.org/)
- [API Keys](https://www.nuget.org/account/apikeys)
- [Your Package](https://www.nuget.org/packages/YitPush) (after publishing)
- [NuGet Documentation](https://learn.microsoft.com/nuget/)

---

**That’s it!** With these manual commands you can publish YitPush in under a minute.