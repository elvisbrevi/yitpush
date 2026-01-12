# Publishing YitPush to NuGet.org

This guide explains how to publish YitPush to NuGet.org so users can install it directly without cloning the repository.

## Prerequisites

1. Create a NuGet.org account at https://www.nuget.org/
2. Generate an API key at https://www.nuget.org/account/apikeys

## Steps to Publish

### 1. Update Version (if needed)

Edit `YitPush/YitPush.csproj` and update the version:

```xml
<Version>1.0.1</Version>
```

### 2. Build and Pack

```bash
cd YitPush
dotnet pack -c Release
```

This creates a `.nupkg` file in `bin/Release/`

### 3. Publish to NuGet.org

```bash
dotnet nuget push bin/Release/YitPush.1.0.0.nupkg \
  --api-key YOUR_API_KEY \
  --source https://api.nuget.org/v3/index.json
```

Replace `YOUR_API_KEY` with your actual NuGet API key.

### 4. Wait for Package to be Available

It may take a few minutes for the package to be indexed and available on NuGet.org.

## After Publishing

Users can then install YitPush globally with a single command:

```bash
dotnet tool install --global YitPush
```

No need to clone the repository or specify a local source!

## Updating the Package

1. Update the version in `YitPush.csproj`
2. Build and pack again
3. Push the new version to NuGet.org

## Best Practices

- **Semantic Versioning**: Use semantic versioning (MAJOR.MINOR.PATCH)
  - MAJOR: Breaking changes
  - MINOR: New features, backwards compatible
  - PATCH: Bug fixes

- **Release Notes**: Add release notes to your GitHub releases

- **Testing**: Test the package locally before publishing:
  ```bash
  dotnet tool install --global --add-source ./bin/Release YitPush
  ```

## Recommended .csproj Additions

Consider adding these properties for better NuGet listing:

```xml
<PropertyGroup>
  <PackageProjectUrl>https://github.com/yourusername/yitpush</PackageProjectUrl>
  <RepositoryUrl>https://github.com/yourusername/yitpush</RepositoryUrl>
  <RepositoryType>git</RepositoryType>
  <PackageTags>git;commit;ai;deepseek;automation;cli;tool</PackageTags>
  <PackageLicenseExpression>MIT</PackageLicenseExpression>
  <PackageReadmeFile>README.md</PackageReadmeFile>
</PropertyGroup>

<ItemGroup>
  <None Include="README.md" Pack="true" PackagePath="\" />
</ItemGroup>
```

## Unpublishing

To unlist (not delete) a package version:

```bash
dotnet nuget delete YitPush 1.0.0 \
  --api-key YOUR_API_KEY \
  --source https://api.nuget.org/v3/index.json
```

Note: Packages cannot be deleted entirely, only unlisted.
