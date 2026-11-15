namespace Omni.Core.Detection;

public enum ToolchainKind
{
    DotNet,
    Node,
    Python,
    Rust,
    Go,
    Java,
    Make,
    CMake,
    PHP,
    Ruby,
    DockerCompose,
    Generic
}

public record DiscoveredProject(
    string Name,
    ToolchainKind Kind,
    string Path,
    string RecommendedDevCommand,
    string RecommendedBuildCommand
);

public class WorkspaceDetector
{
    private static readonly string[] IgnoredDirectories = { 
        ".git", "node_modules", "bin", "obj", ".vs", ".idea", "dist", "build", ".venv", "vendor", "target" 
    };

    public List<DiscoveredProject> Detect(string rootDirectory)
    {
        var results = new List<DiscoveredProject>();

        if (!Directory.Exists(rootDirectory))
            return results;

        // 1. Root Level Checks
        CheckDirectory(rootDirectory, isRoot: true, results);

        // 2. Scan subdirectories (1 level deep)
        var subDirs = Directory.GetDirectories(rootDirectory);
        foreach (var dir in subDirs)
        {
            var dirName = Path.GetFileName(dir);
            if (IgnoredDirectories.Contains(dirName))
                continue;

            CheckDirectory(dir, isRoot: false, results);
        }

        return results;
    }

    private void CheckDirectory(string dir, bool isRoot, List<DiscoveredProject> results)
    {
        var name = isRoot ? "root" : Path.GetFileName(dir);

        // Rust
        if (File.Exists(Path.Combine(dir, "Cargo.toml")))
        {
            results.Add(new DiscoveredProject(name, ToolchainKind.Rust, dir, "cargo run", "cargo build --release"));
            return;
        }

        // Go
        if (File.Exists(Path.Combine(dir, "go.mod")))
        {
            results.Add(new DiscoveredProject(name, ToolchainKind.Go, dir, "go run .", "go build -v ."));
            return;
        }

        // Node.js / Frontend
        if (File.Exists(Path.Combine(dir, "package.json")))
        {
            results.Add(new DiscoveredProject(name, ToolchainKind.Node, dir, "npm run dev", "npm run build"));
            return;
        }

        // Python
        if (File.Exists(Path.Combine(dir, "pyproject.toml")) || File.Exists(Path.Combine(dir, "requirements.txt")))
        {
            results.Add(new DiscoveredProject(name, ToolchainKind.Python, dir, "python -m uvicorn main:app --reload", "python -m build"));
            return;
        }

        // Java / Kotlin (Maven / Gradle)
        if (File.Exists(Path.Combine(dir, "pom.xml")))
        {
            results.Add(new DiscoveredProject(name, ToolchainKind.Java, dir, "mvn spring-boot:run", "mvn package"));
            return;
        }
        if (File.Exists(Path.Combine(dir, "build.gradle")) || File.Exists(Path.Combine(dir, "build.gradle.kts")))
        {
            results.Add(new DiscoveredProject(name, ToolchainKind.Java, dir, "gradle bootRun", "gradle build"));
            return;
        }

        // C / C++ (Make / CMake)
        if (File.Exists(Path.Combine(dir, "CMakeLists.txt")))
        {
            results.Add(new DiscoveredProject(name, ToolchainKind.CMake, dir, "cmake --build build", "cmake -B build && cmake --build build"));
            return;
        }
        if (File.Exists(Path.Combine(dir, "Makefile")))
        {
            results.Add(new DiscoveredProject(name, ToolchainKind.Make, dir, "make run", "make"));
            return;
        }

        // PHP
        if (File.Exists(Path.Combine(dir, "composer.json")))
        {
            results.Add(new DiscoveredProject(name, ToolchainKind.PHP, dir, "php -S localhost:8000", "composer install"));
            return;
        }

        // .NET / C#
        var sln = Directory.EnumerateFiles(dir, "*.sln*", SearchOption.TopDirectoryOnly).FirstOrDefault();
        var csproj = Directory.EnumerateFiles(dir, "*.csproj", SearchOption.TopDirectoryOnly).FirstOrDefault();
        if (sln != null || csproj != null)
        {
            results.Add(new DiscoveredProject(name, ToolchainKind.DotNet, dir, "dotnet watch run", "dotnet build -c Release"));
            return;
        }

        // Docker Compose
        if (isRoot)
        {
            var compose = Directory.EnumerateFiles(dir, "*compose*.y*ml", SearchOption.TopDirectoryOnly).FirstOrDefault();
            if (compose != null)
            {
                results.Add(new DiscoveredProject("infra-compose", ToolchainKind.DockerCompose, dir, "docker compose up", "docker compose build"));
            }
        }
    }
}
