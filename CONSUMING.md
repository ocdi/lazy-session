Consuming the package

1. Add the GitHub Packages feed for the owner to your consuming project's `NuGet.config` or add it via CLI. Example:

```bash
dotnet nuget add source --name github "https://nuget.pkg.github.com/OWNER/index.json" \
  --username OWNER --password <PAT> --store-password-in-clear-text
```

2. Add the package reference in your project (replace package id and version):

```xml
<ItemGroup>
  <PackageReference Include="LazySession" Version="1.0.0" />
</ItemGroup>
```

Notes:

- Replace `<PAT>` with a Personal Access Token that has `read:packages` (or `packages: read`) scope, or configure authentication using GitHub Actions `GITHUB_TOKEN` inside workflows.
- If your package id or owner differs (e.g., `CustomSession`), use that package id in the `PackageReference`.
- You can also add the feed to a repository-level `NuGet.config` checked into the consuming repo to simplify CI consumption.
