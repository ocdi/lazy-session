# Publishing NuGet package from CI

This repository includes a GitHub Actions workflow that builds, packs, and publishes the `CustomSession` NuGet package to the repository's GitHub Packages feed when a tag `v*` is pushed or when a release is published.

- Workflow file: `.github/workflows/nuget-publish.yml`

Notes:

- The workflow uses the provided `GITHUB_TOKEN` and requires the workflow permissions `packages: write` (already configured in the workflow).
- For manual pushes from a local machine you can use a Personal Access Token (PAT) with `write:packages` scope and push to `https://nuget.pkg.github.com/<OWNER>/index.json`.

To publish a release (recommended):

1. Create a tag like `v1.0.0` and push it to GitHub, or create a GitHub Release via the UI.
2. The workflow will run and publish the created `.nupkg` to GitHub Packages.
