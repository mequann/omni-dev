# Omni ⚡

> **Intelligent Polyglot Dev Orchestrator & Observability Engine**  
> Run your entire microservice & frontend stack concurrently with zero configuration—and get instant, human-readable explanations whenever anything fails.

[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)
[![.NET 9 & 10](https://img.shields.io/badge/.NET-9.0%20%7C%2010.0-purple.svg)](https://dotnet.microsoft.com/)
[![npm version](https://img.shields.io/npm/v/omni-dev.svg)](https://www.npmjs.com/package/omni-dev)
[![NuGet version](https://img.shields.io/nuget/v/OmniDev.Cli.svg)](https://www.nuget.org/packages/OmniDev.Cli)

---

## 💡 Why Omni?

Modern applications are rarely built with just one language or service. A typical repository might have:
* An **ASP.NET Core**, **Go**, or **NestJS** backend
* A **Next.js** / **React** frontend
* A **Python** worker or AI pipeline
* A **PostgreSQL** or Redis dependency

### The Problem
When running multi-service setups with standard tools:
1. **Log Chaos**: Stderr and stdout from 5 different services interleave into an unreadable wall of text.
2. **Terminal Clutter**: Developers end up juggling 4 different terminal tabs or split panes manually.
3. **Silent or Mysterious Crashes**: A service fails deep into startup, and developers waste 20 minutes manually hunting through stack traces to figure out *why*.
4. **Common Friction Points**: Port collisions (`EADDRINUSE`), lingering background processes, missing packages (`ModuleNotFoundError`, `Cannot find module`), unapplied migrations, or disconnected dependencies kill developer velocity.

### The Omni Solution
Omni acts as the **intelligent layer** sitting above your existing toolchains (MSBuild, npm, pnpm, cargo, python, go, docker):

* 🚀 **Zero-Config Monorepo Discovery**: Automatically inspects workspace roots and sub-folders (`apps/*`, `packages/*`, `services/*`), discovering runtimes, package scripts, and dev commands.
* ⚡ **Concurrent Dev Runner**: Runs all services concurrently with color-coded prefixes, unified lifecycle management, and graceful teardown.
* 🖥️ **Interactive Multi-Pane TUI Dashboard**: View logs side-by-side in divided terminal panes without switching tabs.
* 🛡️ **Intelligent Port Conflict Remapping**: If a port is occupied, Omni automatically detects the conflict and remaps to the next free port instead of crashing.
* 🔍 **Smart Observability & Root-Cause Explanations**: When a service crashes or fails to build, Omni intercepts the error, isolates the signal from the noise, and tells you **plainly why it happened** and **how to fix it**.

---

## 🌟 Key Features

### 1. Interactive Multi-Pane TUI Dashboard (`omni dev --split`)
Avoid terminal switching and messy log interleaving. Omni divides your terminal into dedicated panels with real-time status badges and keyboard shortcuts:

```text
╭─ [api] (4000) ● Live ──────────╮ ╭─ [web] (3000) ● Live ──────────╮
│ [Nest] Starting Nest app...    │ │ - Local: http://localhost:3000 │
│ [Nest] AppModule initialized   │ │ - Ready in 1.4s                │
│ [Nest] Mapped /api/auth/login  │ │ GET /dashboard 200 in 34ms     │
│ [Nest] Mapped /api/students    │ │ POST /api/auth/login 200 in 61 │
│ [Nest] API listening on :4000  │ │                                │
╰────────────────────────────────╯ ╰────────────────────────────────╯
┌─ Omni Multi-Pane TUI │ Shortcuts: [o] Open Browser │ [c] Clear │ [q / Ctrl+C] Stop ─┐
```

* **Dynamic Layouts**: Automatically splits side-by-side for 2 services, or a 2x2 grid for 3+ services.
* **Interactive Shortcuts**:
  * Press **`o`** to launch all active services in your default browser.
  * Press **`c`** to clear logs across all panes.
  * Press **`q`** or **`Ctrl+C`** to gracefully stop all processes.

---

### 2. Intelligent Observability & Root-Cause Explanations
Instead of raw, cryptic error dumps, Omni analyzes failure output and provides immediate context:

```text
┌─ Diagnostic Failure in 'web' (Exit code: 1) ────────────────────────────────────────────────┐
│ Issue: Port Collision: Next.js dev server already active (PID: 28872)                       │
│                                                                                             │
│ 💡 Why it happened: Another Next.js dev server instance is already running and bound        │
│ to port 3000 (Process ID: 28872).                                                           │
│                                                                                             │
│ 👉 Suggested Fix: Terminate the lingering instance with 'taskkill /PID 28872 /F' on Windows │
│ (or 'kill -9 28872' on Unix), or run with omni dev (which automatically remaps free ports). │
└─────────────────────────────────────────────────────────────────────────────────────────────┘
```

Common issues Omni diagnoses automatically:
* **Port Collisions & Lingering Servers** (`EADDRINUSE`, Next.js duplicate instances, occupied sockets)
* **Missing Package Scripts** (`Missing script: "dev"` in `package.json` with remediation guidance)
* **Missing Dependencies** (`Cannot find module`, `ModuleNotFoundError`, `dotnet restore` failures)
* **Unreachable Infrastructure** (PostgreSQL, Redis, RabbitMQ connection refused)
* **Missing Environment Variables** (`KeyError`, undefined `.env` variables)
* **Compilation & Build Failures** (highlights exact file, line, and syntax issue)

---

### 3. Dynamic Port Conflict Remapping
Never suffer from `EADDRINUSE` again:
* When a service port (e.g. `3000` or `4000`) is already occupied, Omni detects it, automatically finds the **next available free port** (e.g. `3001` or `4001`), and injects `PORT=<freePort>` into the service's environment.
* Prefer killing lingering processes? Simply pass the `--kill` flag:
  ```bash
  omni dev --kill
  ```

---

### 4. First-Class `omni build` Command
Build all projects across your polyglot stack concurrently with zero configuration:
```bash
omni build
```
* **Zero-Config Discovery**: Automatically discovers all buildable projects (`npm run build`, `dotnet build`, `cargo build`, `go build`, `mvn package`).
* **Failure Analysis**: If any project fails to build, Omni intercepts compiler errors (C#, TypeScript, Rust, etc.) and provides instant root-cause diagnostics.
* **Summary Table**: Displays per-target durations and build outcomes.

---

### 5. URL Dashboard & Browser Launching (`--open` and `omni open`)
Omni prints a clean, clickable dashboard banner on startup with service URLs and Swagger API documentation links:

```text
🚀 Services Ready & Starting Parallel Execution:
╭─────────┬───────────────────────┬───────────┬───────────────────╮
│ Service │ Local URL             │ Directory │ Dev Command       │
├─────────┼───────────────────────┼───────────┼───────────────────┤
│ api     │ http://localhost:4000 │ apps/api  │ npm run start:dev │
│ web     │ http://localhost:3000 │ apps/web  │ npm run dev       │
╰─────────┴───────────────────────┴───────────┴───────────────────╯
📖 API Swagger Docs: http://localhost:4000/api/docs or http://localhost:4000/swagger
```

* Run `omni dev --open` to automatically launch services in your default browser on startup.
* Run `omni open` at any time to open active service URLs in the browser.

---

### 6. Auto-Discovery & Zero Configuration
Works out of the box across polyglot and monorepo projects:
* **Node.js / Frontend**: `npm run dev` / `pnpm dev` / `yarn dev` / `bun dev` (Next.js, Vite, React, NestJS)
* **.NET**: `dotnet watch run` / `dotnet run`
* **Python**: `fastapi dev` / `uvicorn` / `python main.py`
* **Rust**: `cargo run` / `cargo watch`
* **Go**: `go run .` / `air`
* **Docker**: `docker compose up`

Need custom options? Drop an `omni.yaml` in your root for granular port, environment, and task control.

---

## 🚀 Quick Start

### Installation

Choose the method that fits your environment:

#### Option 1: .NET Global Tool (Recommended — Any OS with .NET 9 or 10)
```bash
dotnet tool install --global OmniDev.Cli
```
*(To update existing installation: `dotnet tool update --global OmniDev.Cli`)*

#### Option 2: npx (Zero Installation — Any OS with Node.js)
```bash
npx omni-dev dev
```

#### Option 3: Standalone Shell Installer (macOS & Linux — No runtimes required)
```bash
curl -fsSL https://raw.githubusercontent.com/mequann/omni-dev/main/install.sh | bash
```

#### Option 4: Standalone PowerShell Installer (Windows — No runtimes required)
```powershell
irm https://raw.githubusercontent.com/mequann/omni-dev/main/install.ps1 | iex
```

---

### Basic Commands

```bash
# 1. Start all services concurrently (standard continuous log stream)
omni dev

# 2. Launch in interactive multi-pane split terminal (TUI)
omni dev --split

# 3. Start services and automatically open in default browser
omni dev --open

# 4. Compile and build all workspace projects with failure diagnostics
omni build

# 5. Open running service URLs directly in default browser
omni open

# 6. Analyze your workspace to see detected services & ports
omni analyze

# 7. Run system health & pre-flight checks (.NET, Node, Python, Docker, ports)
omni doctor

# 8. Generate an omni.yaml configuration template
omni init

# 9. Execute defined tasks (e.g. test, lint)
omni run test
```

---

## ⚙️ Configuration (`omni.yaml`)

While Omni works with zero config, you can define explicit rules in `omni.yaml`:

```yaml
version: "1.0"
name: "my-polyglot-app"

services:
  api:
    command: npm run start:dev
    dir: ./apps/api
    color: magenta
    port: 4000
    env:
      NODE_ENV: development

  web:
    command: npm run dev
    dir: ./apps/web
    color: cyan
    port: 3000
    dependsOn:
      - api

tasks:
  setup:
    commands:
      - npm --prefix apps/api install
      - npm --prefix apps/web install

  db-up:
    commands:
      - docker compose up -d --wait postgres

  build:
    commands:
      - npm --prefix apps/api run build
      - npm --prefix apps/web run build

  test:
    parallel: true
    commands:
      - npm --prefix apps/api run test
      - npm --prefix apps/web run test
```

---

## 📚 Documentation

Detailed guides and specifications:

* 🚀 **[Getting Started Guide](docs/getting-started.md)** – 60-second quickstart, zero config usage, and basic workflows.
* ⚙️ **[`omni.yaml` Configuration Guide](docs/configuration-guide.md)** – Complete reference for ports, environment variables, dependencies, and custom tasks.
* 🔍 **[Failure Diagnostics & Observability](docs/failure-diagnostics.md)** – In-depth guide on crash pattern recognition, port conflicts, and root-cause explanations.
* 📐 **[Architecture & Design](docs/architecture.md)** – Detailed breakdown of `Omni.Core`, `Omni.Cli`, and process lifecycle management.
* 🤝 **[Contributing Guide](CONTRIBUTING.md)** – How to build from source, add language detectors, and write diagnostic rules.

---

## 🚢 Publishing & Deployment

Omni is configured for multi-channel publishing (see local `DEPLOYMENT.md` for maintainer publishing steps):

1. **Standalone Binaries (GitHub Releases)**: Auto-built for Windows, Linux, and macOS upon pushing a version tag (`git push origin v0.2.0`).
2. **NuGet.org**: Published via GitHub Actions using **Trusted Publishing (OIDC)** or API key (`dotnet tool install -g OmniDev.Cli`).
3. **npm Registry**: Package wrapper in `npm/` ready for `npx omni-dev` via `npm publish ./npm`.

---

## 🗺️ Roadmap

- [x] Polyglot auto-discovery engine (.NET, Node, Python, Go, Rust)
- [x] Concurrent multi-process runner with ANSI stream prefixing
- [x] Interactive Multi-Pane Split Terminal (TUI) Dashboard (`omni dev --split`)
- [x] First-class `omni build` command with compiler error diagnostics
- [x] Dynamic port collision remapping (auto-remap to next free port)
- [x] URL dashboard with Swagger links and automatic browser opening (`--open` / `omni open`)
- [x] Config schema & generator (`omni.yaml`)
- [x] Built-in Failure Diagnostic Engine (pattern-matching common crash signatures)
- [x] Pre-flight health checks & `omni doctor` (runtime availability & port verification)
- [ ] Cross-tool execution history and bottleneck analysis

---

## 🤝 Contributing & License

Contributions, issues, and feature requests are welcome! Check out our [Contributing Guide](CONTRIBUTING.md) to get started.

Distributed under the **MIT License**. See [LICENSE](LICENSE) for more information.
