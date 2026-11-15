# `omni.yaml` Configuration Guide ⚙️

While Omni works out of the box with zero configuration, you can drop an `omni.yaml` file into your repository root for granular control over ports, working directories, environment variables, dependencies, and custom build/test pipelines.

---

## 📄 Generating a Template

Run:
```bash
omni init
```
This will generate a ready-to-use `omni.yaml` in your current directory.

---

## 📑 Full Configuration Schema Reference

Here is a complete example showing all available fields:

```yaml
version: "1.0"
name: "ecommerce-platform"

# Define the long-running services for `omni dev`
services:
  # Unique service key name
  api:
    path: "./src/Services/Api"        # Working directory
    command: "dotnet watch run"        # Shell command to execute
    port: 5001                         # Declared HTTP/TCP port
    color: "cyan"                      # Terminal badge color (cyan, green, yellow, red, magenta, blue)
    env:
      ASPNETCORE_ENVIRONMENT: "Development"
      DATABASE_URL: "Server=localhost;Port=5432;Database=shop;User Id=postgres;Password=secret;"

  web:
    path: "./apps/web"
    command: "npm run dev"
    port: 3000
    color: "green"
    dependsOn:
      - api                            # Starts after the api service begins
    env:
      NEXT_PUBLIC_API_URL: "http://localhost:5001"

  worker:
    path: "./services/recommendations"
    command: "python -m uvicorn main:app --reload --port 8000"
    port: 8000
    color: "magenta"
    env:
      REDIS_HOST: "localhost"

# Define reproducible build, test, and lint tasks for `omni run <task>`
tasks:
  # Task executed via `omni run build`
  build:
    parallel: true                     # Run all commands simultaneously
    commands:
      - "dotnet build ./src/Services/Api/Api.csproj"
      - "npm run build --prefix ./apps/web"

  # Task executed via `omni run test`
  test:
    parallel: false                    # Run sequentially (fails fast on first error)
    commands:
      - "dotnet test"
      - "npm run test --prefix ./apps/web"

  # Custom pipeline task: `omni run db:reset`
  db:reset:
    parallel: false
    commands:
      - "docker compose restart postgres"
      - "dotnet ef database update --project ./src/Services/Api"
```

---

## 🎨 Supported Colors

Badge colors supported by Spectre.Console:
* `cyan`
* `green`
* `yellow`
* `blue`
* `magenta`
* `red`
* `orange1`
* `deepskyblue1`

---

## 🔄 Task Execution Modes

* **`parallel: true`**: Commands execute concurrently across thread pools. Ideal for multi-project builds, linting, and type checking.
* **`parallel: false`**: Commands execute sequentially in list order. Ideal for migrations, setup steps, or pipelines where order matters.

