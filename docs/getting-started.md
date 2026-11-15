# Getting Started with Omni 🚀

Omni is designed to get you from zero to running all your services concurrently in less than 60 seconds—without writing custom shell scripts or fighting conflicting process windows.

---

## ⚡ 60-Second Quickstart

### Step 1: Install Omni
Install Omni as a global .NET tool:
```bash
dotnet tool install --global OmniDev.Cli
```

### Step 2: Navigate to Any Project
Go to your project repository (monorepo, microservices, or polyglot app):
```bash
cd my-polyglot-workspace
```

### Step 3: Run All Services Concurrently
```bash
omni dev
```
Omni automatically scans your directory, discovers your backends, frontends, and workers, and spins them up concurrently with color-coded log prefixes and live process management.

---

## 🔍 Core Commands Overview

| Command | Purpose |
| :--- | :--- |
| `omni dev` | Starts all services concurrently with smart failure diagnostics. |
| `omni analyze` | Scans workspace and prints discovered projects and runtimes. |
| `omni doctor` | Validates host tools (`dotnet`, `node`, `python`, `docker`) and checks for port conflicts. |
| `omni init` | Creates an annotated `omni.yaml` to customize commands, ports, and env variables. |
| `omni run <task>` | Runs pipeline tasks (e.g. `omni run test`, `omni run build`) sequentially or in parallel. |

---

## 🛠 Real-World Example: Polyglot Workspace

Imagine your repo looks like this:
```text
my-repo/
├── backend/            # ASP.NET Core API (.csproj)
├── frontend/           # Next.js app (package.json)
└── ai-worker/          # Python FastAPI service (requirements.txt)
```

### Zero Configuration Mode
Without creating any config file, run:
```bash
omni dev
```
Omni automatically runs:
1. `backend`: `dotnet watch run` (prefixed in purple)
2. `frontend`: `npm run dev` (prefixed in green)
3. `ai-worker`: `uvicorn app:main --reload` (prefixed in blue)

All output is cleanly interleaved with color tags, and pressing `Ctrl+C` cleanly shuts down all three processes without leaving zombie node or dotnet processes holding open ports.

---

## 💡 What Happens When a Service Crashes?

If a service fails during startup (e.g., port 3000 is already in use by another app):

```text
╭── ❌ Diagnostic Failure in 'frontend' (Exit code: 1) ─────────────╮
│ Issue: Port Collision: Port 3000 is already occupied              │
│                                                                   │
│ 💡 Why it happened:                                               │
│   Service attempted to listen on port 3000, but another process   │
│   is already bound to it.                                         │
│                                                                   │
│ 👉 Suggested Fix:                                                 │
│   Terminate the process using port 3000, or configure a different  │
│   port in omni.yaml under services.frontend.port                  │
╰───────────────────────────────────────────────────────────────────╯
```

Instead of scrolling through 200 lines of console noise, Omni gives you the exact reason and the fix.

