# Omni Deployment & Publishing Guide 🚀

Congratulations on publishing to **npm**! Here is the crystal-clear guide for the remaining two channels:

---

## Channel 1: Standalone Binaries (GitHub Releases)

### What it does:
Creates the downloadable files (`omni.exe` for Windows, `omni` for Mac & Linux) on your GitHub repository under **Releases**.

### How to do it (Only 2 Commands):
You don't need to build anything manually on your machine. GitHub Actions does it automatically in the cloud.

Run these two commands in your terminal:
```bash
git tag v0.1.1
git push origin v0.1.1
```

### Where to see it:
1. Open: **[https://github.com/mequann/omni-dev/actions](https://github.com/mequann/omni-dev/actions)** to watch the cloud build.
2. In ~3 minutes, open: **[https://github.com/mequann/omni-dev/releases](https://github.com/mequann/omni-dev/releases)**
3. You will see the release with all four downloadable archives:
   * `omni-windows-x64.zip`
   * `omni-linux-x64.tar.gz`
   * `omni-macos-arm64.tar.gz`
   * `omni-macos-x64.tar.gz`

---

## Channel 2: NuGet.org (.NET Global Tool)

### What it does:
Allows developers to install Omni via `dotnet tool install --global Omni.Cli`.

### The Easiest Method: Drag & Drop via Web Browser (No API keys needed!)

1. **Step 1: Generate the `.nupkg` package on your computer**
   Run this in your repository root:
   ```bash
   dotnet pack src/Omni.Cli -c Release
   ```
   This generates a package file at:
   `src/Omni.Cli/bin/Release/Omni.Cli.0.1.0.nupkg`

2. **Step 2: Upload to NuGet.org**
   * Open your browser and go to: **[https://www.nuget.org/packages/manage/upload](https://www.nuget.org/packages/manage/upload)**
   * Log in (with Microsoft or GitHub).
   * Click **Browse** or drag and drop the file `Omni.Cli.0.1.0.nupkg` into the upload box.
   * Review the package details and click **Submit**.

3. **Step 3: Verification**
   After ~5 minutes, open any terminal and test:
   ```bash
   dotnet tool install --global Omni.Cli
   omni --version
   ```

---

### Alternative Method: Publish via Terminal Command

If you prefer using the command line instead of the browser upload:
1. Go to **[https://www.nuget.org/account/apikeys](https://www.nuget.org/account/apikeys)**.
2. Click **Create**, name it `omni-key`, and click **Copy**.
3. Run this command (replacing `<YOUR_KEY>` with your copied key):
   ```bash
   dotnet nuget push src/Omni.Cli/bin/Release/Omni.Cli.*.nupkg --api-key <YOUR_KEY> --source https://api.nuget.org/v3/index.json
   ```

---

## Channel 3: npm (`npx omni-dev`) — Already Done! 🎉

Users can now run Omni instantly:
```bash
npx omni-dev dev
```
