# Failure Diagnostics & Observability 🔍

One of the biggest pain points in multi-service development is debugging process failures when running 3+ applications concurrently. Standard terminal runners interleave thousands of log lines, meaning errors often flash by unnoticed or are buried in verbosity.

Omni addresses this with an integrated **Observability & Diagnostic Engine**.

---

## 🧠 How the Diagnostic Engine Works

```
[ Process Running ] 
       │
       ▼ (Process Exits with Non-Zero Code)
[ Rolling Line Buffer Snapshot ] (Last 50 stdout/stderr lines)
       │
       ▼
[ FailureAnalyzer Engine ]
       ├── Pattern Matchers (Ports, Packages, Connections, PATH, Compilers)
       ├── Context Isolator
       └── Remediation Synthesizer
       │
       ▼
[ Spectre.Console Diagnostic Card ] (Why it happened + How to fix it)
```

---

## 🎯 Built-In Failure Signatures

Omni recognizes and provides human-readable explanations for common developer failures:

### 1. Port Collisions (`PortCollision`)
* **Trigger Signatures**: `EADDRINUSE`, `address already in use`, `Only one usage of each socket address`, `Failed to bind to address`.
* **Diagnosis**:
  * Extracts the specific port number.
  * Informs the developer that another process is occupying the address.
* **Suggested Fix**: Identifies how to terminate the occupying process on Windows/macOS/Linux or how to change the port in `omni.yaml`.

---

### 2. Missing Packages & Modules (`MissingDependency`)
* **Trigger Signatures**:
  * Node: `Cannot find module '<module>'`
  * Python: `ModuleNotFoundError: No module named '<module>'`
  * .NET: `NuGet package restore failed` or `error NU1101`
* **Diagnosis**: Points out the exact missing package or library name.
* **Suggested Fix**: Recommends the exact installation command (`npm install`, `pip install <module>`, `dotnet restore`).

---

### 3. Connection Refused (`ConnectionRefused`)
* **Trigger Signatures**: `ECONNREFUSED`, `Connection refused`, `could not connect to server`, `dial tcp: connection refused`.
* **Diagnosis**: Notifies that an upstream service (Postgres, MySQL, Redis, RabbitMQ) is offline or unreachable on the requested endpoint.
* **Suggested Fix**: Recommends checking Docker container status or local service daemons.

---

### 4. Missing Runtime CLI (`MissingRuntime`)
* **Trigger Signatures**:
  * Windows: `'<cmd>' is not recognized as an internal or external command`
  * Linux/macOS: `command not found: <cmd>`, `/bin/sh: line 1: <cmd>: not found`
* **Diagnosis**: Explains that the runtime executable is not installed or missing from the user's `$PATH`.
* **Suggested Fix**: Recommends installing the tool or verifying environment variables.

---

### 5. Build & Compilation Breakages (`BuildOrSyntaxError`)
* **Trigger Signatures**: `error CSxxxx`, `SyntaxError:`, `TypeError:`, `cargo: error`.
* **Diagnosis**: Strips surrounding log clutter and highlights the exact syntax error and file reference.

---

## 🩺 Preflight Checking (`omni doctor`)

You can run diagnostics proactively before launching any processes:

```bash
omni doctor
```

This verifies:
1. All host toolchains (`dotnet`, `node`, `npm`, `python`, `cargo`, `go`, `docker`, `git`) and outputs their active versions.
2. Checks all ports declared in `omni.yaml` to ensure none are already in conflict before you attempt to launch your stack.

