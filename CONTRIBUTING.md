# Contributing to Omni 🤝

Thank you for your interest in contributing to Omni! We welcome issues, documentation improvements, new language detectors, and diagnostic failure rules.

---

## 🛠 Local Development Setup

### Prerequisites
* [.NET 9.0 SDK](https://dotnet.microsoft.com/download) (or higher)
* Git

### Clone & Build
```bash
# Clone the repository
git clone git@github-personal:mequann/omni-dev.git
cd omni-dev

# Build the entire solution
dotnet build

# Run unit tests
dotnet test
```

### Running Omni Locally
You can run the CLI directly from source:
```bash
# View help
dotnet run --project src/Omni.Cli --help

# Run doctor check
dotnet run --project src/Omni.Cli doctor

# Run workspace analysis
dotnet run --project src/Omni.Cli analyze
```

---

## 💡 Adding a New Language / Toolchain Detector
To add support for a new language (e.g. Ruby, Elixir, PHP):
1. Open [`src/Omni.Core/Detection/WorkspaceDetector.cs`](src/Omni.Core/Detection/WorkspaceDetector.cs).
2. Add your new enum value to `ToolchainKind`.
3. Add discovery logic checking for project files (e.g. `Gemfile`, `mix.exs`, `composer.json`).
4. Add unit test coverage in `tests/Omni.Core.Tests/WorkspaceDetectorTests.cs`.

---

## 🔍 Adding a New Diagnostic Rule
To add new crash detection patterns:
1. Open [`src/Omni.Core/Diagnostics/FailureAnalyzer.cs`](src/Omni.Core/Diagnostics/FailureAnalyzer.cs).
2. Add your regex pattern and diagnosis mapping in `Analyze()`.
3. Add a unit test verifying the match in `tests/Omni.Core.Tests/FailureAnalyzerTests.cs`.

---

## 📜 Pull Request Guidelines
1. Ensure all unit tests pass (`dotnet test`).
2. Keep commits atomic and descriptive.
3. Open a Pull Request on GitHub against the `main` branch.

