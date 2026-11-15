# Omni Architecture & Design 📐

Omni is built in modern C# (.NET 9 / .NET 10 preview) following clean modular architecture principles:

---

## 🏗 High-Level Architecture

```
┌─────────────────────────────────────────────────────────────┐
│                          Omni.Cli                           │
│     (Spectre.Console CLI, Command Routing, Formatting)      │
├─────────────────────────────────────────────────────────────┤
│                         Omni.Core                           │
│  ┌──────────────────────┬────────────────────────────────┐  │
│  │   WorkspaceDetector  │           OmniConfig           │  │
│  │ (Auto-detect stacks) │   (YAML Config & Schema)       │  │
│  ├──────────────────────┼────────────────────────────────┤  │
│  │  ConcurrentManager   │         ProcessRunner          │  │
│  │ (Process coordinator)│  (ProcessTree kill & stream)   │  │
│  ├──────────────────────┴────────────────────────────────┤  │
│  │                   Omni.Diagnostics                    │  │
│  │   FailureAnalyzer · PreflightChecker · FailureModel   │  │
│  └───────────────────────────────────────────────────────┘  │
└─────────────────────────────────────────────────────────────┘
```

---

## 📦 Key Subsystems

### 1. `Omni.Core.Detection`
* **`WorkspaceDetector`**:
  * Traverses directory hierarchies (ignoring `.git`, `bin`, `obj`, `node_modules`).
  * Discovers project types via signature files:
    * `*.csproj`, `*.sln`, `*.slnx` ➔ .NET
    * `package.json` ➔ Node.js (checks for `dev` or `start` scripts)
    * `requirements.txt`, `pyproject.toml` ➔ Python
    * `Cargo.toml` ➔ Rust
    * `go.mod` ➔ Go
    * `docker-compose.yml` ➔ Docker Compose

### 2. `Omni.Core.Execution`
* **`ProcessRunner`**:
  * Platform-agnostic shell launcher (`cmd.exe /c` on Windows, `/bin/sh -c` on Unix/macOS).
  * Asynchronously streams stdout and stderr events.
  * **Process Tree Teardown Guarantee**:
    * On Windows: Uses `taskkill /PID <pid> /T /F` to prevent lingering child processes (like node workers or roslyn compiler nodes).
    * On Unix: Uses `Process.Kill(entireProcessTree: true)`.

### 3. `Omni.Core.DevRunner`
* **`ConcurrentManager`**:
  * Registers multiple `ServiceSpec` instances.
  * Maintains thread-safe circular buffers of the last 50 lines for every service.
  * Dispatches output to terminal multiplexers.
  * Coordinates cancellation across all running services on SIGINT (`Ctrl+C`).

### 4. `Omni.Core.Diagnostics`
* **`FailureAnalyzer`**: Regex pattern engine scanning buffered output when exit codes are non-zero.
* **`PreflightChecker`**: Inspects network TCP sockets (`IPGlobalProperties`) to verify port availability prior to process launch.
