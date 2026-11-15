# Omni Deployment Guide 🚀

This guide provides step-by-step instructions for deploying and publishing Omni across all three supported distribution channels:

1. **Case 1: Standalone Native Binaries & GitHub Releases** (Zero dependencies for end-users)
2. **Case 2: NuGet.org .NET Global Tool** (For .NET developers worldwide)
3. **Case 3: npm Package & npx Runner** (For JavaScript, TypeScript, and Web developers)

---

## 📋 Pre-Deployment Checklist

Before deploying a new version (e.g. `v0.1.0`):

1. **Run full verification tests**:
   ```bash
   dotnet test
   ```
2. **Update version numbers** (if releasing a new version):
   * In `src/Omni.Cli/Omni.Cli.csproj`: `<Version>0.1.0</Version>`
   * In `src/Omni.Core/Omni.Core.csproj`: `<Version>0.1.0</Version>`
   * In `npm/package.json`: `"version": "0.1.0"`

---

## 📦 Case 1: Standalone Native Binaries & GitHub Releases

Standalone binaries bundle the runtime into a single executable so users on Windows, Linux, and macOS don't even need .NET installed.

### Option A: Automated via GitHub Actions (Recommended)

1. **Commit all changes**:
   ```bash
   git add -A
   git commit -m "chore(release): prepare v0.1.0"
   git push origin main
   ```

2. **Create and push the version tag**:
   ```bash
   git tag v0.1.0
   git push origin v0.1.0
   ```

3. **What happens automatically**:
   * GitHub Actions triggers `.github/workflows/release.yml`.
   * It builds:
     * `omni-windows-x64.zip` (Windows executable)
     * `omni-linux-x64.tar.gz` (Linux native binary)
     * `omni-macos-arm64.tar.gz` (Apple Silicon M-series)
     * `omni-macos-x64.tar.gz` (Intel Mac)
   * A GitHub Release is drafted, tagged, and published automatically with release notes and all binary archives attached.

### Option B: Manual Local Compilation

To build a standalone single-file binary on your local machine:

* **For Windows (x64)**:
  ```bash
  dotnet publish src/Omni.Cli -c Release -r win-x64 -f net9.0 --self-contained true -p:PublishSingleFile=true -o ./dist/windows
  ```
* **For Linux (x64)**:
  ```bash
  dotnet publish src/Omni.Cli -c Release -r linux-x64 -f net9.0 --self-contained true -p:PublishSingleFile=true -o ./dist/linux
  ```
* **For macOS (Apple Silicon ARM64)**:
  ```bash
  dotnet publish src/Omni.Cli -c Release -r osx-arm64 -f net9.0 --self-contained true -p:PublishSingleFile=true -o ./dist/macos-arm64
  ```

### Verification
Test the generated standalone binary:
```bash
./dist/windows/omni.exe --version
```

---

## 📦 Case 2: NuGet.org (.NET Global Tool)

Publishing to NuGet allows any developer with .NET to install Omni globally via `dotnet tool install --global Omni.Cli`.

### Option A: Automated via Trusted Publishing (OIDC)

Once you set up a **Trusted Publishing Policy** on NuGet.org:
* **Repository**: `mequann/omni-dev`
* **Workflow**: `release.yml`

Whenever you push a Git tag (`git push origin v0.1.0`), GitHub Actions automatically packs the tool and pushes `Omni.Cli.*.nupkg` directly to NuGet.org without needing manual API keys.

### Option B: Manual Local Publish via API Key

1. Generate an API Key on [nuget.org](https://www.nuget.org/account/apikeys).
2. Pack the release package:
   ```bash
   dotnet pack src/Omni.Cli -c Release -o ./dist/nupkg
   ```
3. Push to NuGet:
   ```bash
   dotnet nuget push ./dist/nupkg/Omni.Cli.*.nupkg --api-key <YOUR_NUGET_API_KEY> --source https://api.nuget.org/v3/index.json --skip-duplicate
   ```

### Verification
Once indexed on NuGet (~5–10 minutes), verify installation on any computer:
```bash
dotnet tool install --global Omni.Cli
omni doctor
```

To update an existing installation:
```bash
dotnet tool update --global Omni.Cli
```

---

## 📦 Case 3: npm Package (`npx omni-dev`)

The `npm/` folder in the repository contains a lightweight Node.js wrapper that allows JavaScript, Next.js, and polyglot developers to run Omni instantly via `npx` or `npm install -g`.

### Step 1: Log in to npm
Make sure you are logged into your npm account in the terminal:
```bash
npm login
```
*(Enter username, password, and one-time password from your email/authenticator).*

### Step 2: Navigate to the `npm` folder
```bash
cd npm
```

### Step 3: Publish to the Public npm Registry
```bash
npm publish --access public
```

### Step 4: Return to the repository root
```bash
cd ..
```

### Verification
Test running directly from npm without installing:
```bash
npx omni-dev --version
npx omni-dev doctor
```

Or install globally via npm:
```bash
npm install -g omni-dev
omni dev
```

---

## 🔄 Summary of Deployment Commands

| Channel | Automated Workflow | Manual Release Command | End-User Install Command |
| :--- | :--- | :--- | :--- |
| **GitHub Binaries** | `git push origin v0.1.0` | `dotnet publish ... -r <rid>` | `curl .../install.sh \| bash`<br>`irm .../install.ps1 \| iex` |
| **NuGet.org** | `git push origin v0.1.0` (OIDC) | `dotnet nuget push ...` | `dotnet tool install -g Omni.Cli` |
| **npm Registry** | *(via tag or manual)* | `cd npm && npm publish --access public` | `npx omni-dev dev`<br>`npm i -g omni-dev` |
