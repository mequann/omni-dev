# Omni Deployment & Publishing Guide 🚀

This document details the exact steps for deploying and publishing **Omni** across all three distribution channels:

1. **Standalone Native Binaries (GitHub Releases)**
2. **NuGet.org (.NET Global Tool)**
3. **npm Package Registry (`npx omni-dev`)**

---

## 📋 Quick Pre-Release Checklist

1. **Verify all tests pass**:
   ```bash
   dotnet test
   ```
2. **Confirm version numbers** match across projects:
   * `src/Omni.Cli/Omni.Cli.csproj`: `<Version>0.1.0</Version>`
   * `src/Omni.Core/Omni.Core.csproj`: `<Version>0.1.0</Version>`
   * `npm/package.json`: `"version": "0.1.0"`

---

## 📦 1. Standalone Binaries (GitHub Releases)

Standalone binaries bundle the runtime into a single executable. End-users do not need .NET, Node, or any runtime installed.

### Automated Publishing (Recommended)
Our GitHub Actions workflow in `.github/workflows/release.yml` automatically compiles and packages all native binaries when you push a version tag:

```bash
git tag v0.1.0
git push origin v0.1.0
```

GitHub Actions will automatically build and publish:
* **Windows (x64)**: `omni-windows-x64.zip` (contains `omni.exe`)
* **Linux (x64)**: `omni-linux-x64.tar.gz` (contains `omni`)
* **macOS (Apple Silicon ARM64)**: `omni-macos-arm64.tar.gz`
* **macOS (Intel x64)**: `omni-macos-x64.tar.gz`

### Manual Local Build (Optional)
* **Windows**:
  ```bash
  dotnet publish src/Omni.Cli -c Release -r win-x64 -f net9.0 --self-contained true -p:PublishSingleFile=true -o ./dist/windows
  ```
* **Linux**:
  ```bash
  dotnet publish src/Omni.Cli -c Release -r linux-x64 -f net9.0 --self-contained true -p:PublishSingleFile=true -o ./dist/linux
  ```
* **macOS (Apple Silicon)**:
  ```bash
  dotnet publish src/Omni.Cli -c Release -r osx-arm64 -f net9.0 --self-contained true -p:PublishSingleFile=true -o ./dist/macos-arm64
  ```

---

## 📦 2. NuGet.org (.NET Global Tool)

Publishing to NuGet allows developers with .NET to install Omni via `dotnet tool install --global Omni.Cli`.

### Automated via Trusted Publishing (OIDC)
Once you configure **Trusted Publishing** on NuGet.org pointing to `mequann/omni-dev` with workflow `release.yml`:
* Every time you push a tag (`git push origin v0.1.0`), GitHub Actions securely mints an OIDC token and uploads the package to NuGet.org automatically.

### Manual Local Publish via API Key
1. Generate an API Key on [nuget.org](https://www.nuget.org/account/apikeys).
2. Pack and push:
   ```bash
   dotnet pack src/Omni.Cli -c Release -o ./dist/nupkg
   dotnet nuget push ./dist/nupkg/Omni.Cli.*.nupkg --api-key <YOUR_NUGET_API_KEY> --source https://api.nuget.org/v3/index.json --skip-duplicate
   ```

---

## 📦 3. npm Registry (`npx omni-dev`)

The `npm/` directory contains the wrapper package for JavaScript, Next.js, and web developers.

### Publishing Steps:
1. Log in to npm in your terminal:
   ```bash
   npm login
   ```
2. Navigate into the `npm` directory:
   ```bash
   cd npm
   ```
3. Publish publicly:
   ```bash
   npm publish --access public
   ```
4. Return to root:
   ```bash
   cd ..
   ```

---

## 🚀 How End-Users Install & Run Omni

| Method | User Command | Prerequisites |
| :--- | :--- | :--- |
| **npm (Zero Install)** | `npx omni-dev dev` | Node.js / npm |
| **npm (Global)** | `npm install -g omni-dev`<br>`omni dev` | Node.js / npm |
| **.NET Tool** | `dotnet tool install --global Omni.Cli`<br>`omni dev` | .NET 9 or 10 SDK |
| **macOS & Linux One-Liner** | `curl -fsSL https://raw.githubusercontent.com/mequann/omni-dev/main/install.sh \| bash` | None |
| **Windows One-Liner** | `irm https://raw.githubusercontent.com/mequann/omni-dev/main/install.ps1 \| iex` | None |
