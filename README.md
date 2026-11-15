# Omni ⚡

> **Intelligent Polyglot Dev Orchestrator & Observability Engine**  
> Run your entire microservice & frontend stack concurrently with zero configuration—and get instant, human-readable explanations whenever anything fails.

[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)
[![.NET 9](https://img.shields.io/badge/.NET-9.0-purple.svg)](https://dotnet.microsoft.com/)

---

## 💡 Why Omni?

Modern applications are rarely built with just one language or service. A typical repository might have:
* An **ASP.NET Core** or **Go** backend
* A **Next.js** / **React** frontend
* A **Python** worker or AI pipeline
* A **PostgreSQL** or Redis dependency

### The Problem
When running multi-service setups with standard tools:
1. **Log Chaos**: Stderr and stdout from 5 different services interleave into an unreadable wall of text.
2. **Silent or Mysterious Crashes**: A service fails deep into startup, and developers waste 20 minutes manually hunting through stack traces to figure out *why*.
3. **Common Friction Points**: Port collisions (`EADDRINUSE`), missing packages (`ModuleNotFoundError`, `Cannot find module`), unapplied migrations, or disconnected dependencies kill developer velocity.

### The Omni Solution
Omni acts as the **intelligent layer** sitting above your existing toolchains (MSBuild, npm, pnpm, cargo, python, go, docker):

* 🚀 **Zero-Config Polyglot Discovery**: Omni inspects your workspace and automatically detects your services, runtime types, ports, and dev commands.
* ⚡ **Concurrent Dev Runner**: Runs all services concurrently with color-coded prefixes, unified process lifecycle management, and graceful teardown.
* 🔍 **Intelligent Observability & Failure Explanations**: When a service crashes or fails to build, Omni intercepts the error, isolates the signal from the noise, and tells you **plainly why it happened** and **how to fix it**.

---

## 🌟 Key Features

### 1. Smart Observability & Root-Cause Explanations
Instead of raw, cryptic error dumps, Omni analyzes failure output and provides immediate context:

```text
❌ [api] Process exited unexpectedly with code 1

💡 Why it happened:
   Port 5001 is already in use by another process (PID: 18420 - node.exe).

👉 Suggested Fix:
   - Terminate the lingering process: Stop-Process -Id 18420
   - Or configure a different port in omni.yaml under services.api.env
```

Common issues Omni diagnoses automatically:
* **Port Collisions** (e.g. `EADDRINUSE`, `address already in use`)
* **Missing Dependencies** (e.g. `npm install` or `pip install` required)
* **Unreachable Infrastructure** (e.g. database/redis connection refused)
* **Missing Environment Variables** (.env file missing or key undefined)
* **Compilation & Build Failures** (highlights the exact file, line, and syntax issue)

### 2. Auto-Discovery & Zero Configuration
Works out of the box across polyglot projects:
* **.NET**: `dotnet watch run` / `dotnet run`
* **Node.js**: `npm run dev` / `pnpm dev` / `yarn dev` / `bun dev`
* **Python**: `fastapi dev` / `uvicorn` / `python main.py`
* **Rust**: `cargo run` / `cargo watch`
* **Go**: `go run .` / `air`
* **Docker**: `docker compose up`

Need custom options? Drop an `omni.yaml` in your root for granular port, environment, and task control.

### 3. Unified Developer Experience
* **Prefix Streaming**: Clean, color-coded live logs tagged by service name.
* **Synchronized Teardown**: Hitting `Ctrl+C` cleans up all spawned sub-processes cleanly—no orphan background tasks hogging memory or ports.

---

## 🚀 Quick Start

### Installation

```bash
# Install Omni as a .NET Global Tool
dotnet tool install --global Omni.Cli
```

### Basic Commands

```bash
# 1. Analyze your workspace to see detected services
omni analyze

# 2. Start all services concurrently with live diagnostics
omni dev

# 3. Run a specific service only
omni run <service-name>

# 4. Generate an omni.yaml configuration template
omni init
```

---

## ⚙️ Configuration (`omni.yaml`)

While Omni works with zero config, you can define explicit rules in `omni.yaml`:

```yaml
version: "1.0"
name: "my-polyglot-app"

services:
  backend:
    path: "./src/Server"
    command: "dotnet watch run"
    port: 5000
    env:
      ASPNETCORE_ENVIRONMENT: "Development"

  frontend:
    path: "./src/Client"
    command: "npm run dev"
    port: 3000
    dependsOn:
      - backend

  ai-service:
    path: "./services/ml"
    command: "uvicorn app:main --reload --port 8000"
    port: 8000
```

---

## 🗺️ Roadmap

- [x] Polyglot auto-discovery engine (.NET, Node, Python, Go, Rust)
- [x] Concurrent multi-process runner with ANSI stream prefixing
- [x] Config schema & generator (`omni.yaml`)
- [ ] Built-in Failure Diagnostic Engine (pattern-matching common crash signatures)
- [ ] Interactive TUI dashboard (switch between service logs, restart individual services with hotkeys)
- [ ] Pre-flight health checks (verify dependencies, ports, and databases before launch)
- [ ] Cross-tool execution history and bottleneck analysis

---

## 🤝 Contributing & License

Contributions, issues, and feature requests are welcome!

Distributed under the **MIT License**. See [LICENSE](LICENSE) for more information.

