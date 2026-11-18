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

        // 1. Scan subdirectories (up to 2 levels deep for monorepos)
        var subDirs = Directory.GetDirectories(rootDirectory);
        foreach (var dir in subDirs)
        {
            var dirName = Path.GetFileName(dir);
            if (IgnoredDirectories.Contains(dirName))
                continue;

            int countBefore = results.Count;
            CheckDirectory(dir, isRoot: false, results);

            // If this directory was not a project itself (e.g. apps, packages, services), scan its children
            if (results.Count == countBefore)
            {
                try
                {
                    var nestedDirs = Directory.GetDirectories(dir);
                    foreach (var nested in nestedDirs)
                    {
                        var nestedName = Path.GetFileName(nested);
                        if (IgnoredDirectories.Contains(nestedName))
                            continue;

                        CheckDirectory(nested, isRoot: false, results);
                    }
                }
                catch { }
            }
        }

        // 2. Root Level Checks
        var rootProjects = new List<DiscoveredProject>();
        CheckDirectory(rootDirectory, isRoot: true, rootProjects);

        if (rootProjects.Count > 0)
        {
            var rootProj = rootProjects[0];
            // If subprojects were found, only add root if root has its own direct dev script (not just a monorepo container)
            if (results.Count == 0)
            {
                results.Add(rootProj);
            }
            else if (rootProj.Kind != ToolchainKind.Node || HasDirectDevScript(rootDirectory))
            {
                results.Insert(0, rootProj);
            }
        }

        return results;
    }

    private static (string devCmd, string buildCmd) ResolveNodeScripts(string pkgJsonPath)
    {
        try
        {
            var text = File.ReadAllText(pkgJsonPath);
            if (text.Contains("\"dev\":") || text.Contains("\"dev\" :"))
                return ("npm run dev", "npm run build");
            if (text.Contains("\"start:dev\":") || text.Contains("\"start:dev\" :"))
                return ("npm run start:dev", "npm run build");
            if (text.Contains("\"start\":") || text.Contains("\"start\" :"))
                return ("npm start", "npm run build");
        }
        catch { }
        return ("npm run dev", "npm run build");
    }

    private static bool HasDirectDevScript(string rootDir)
    {
        try
        {
            var pkgJson = Path.Combine(rootDir, "package.json");
            if (!File.Exists(pkgJson)) return false;
            var text = File.ReadAllText(pkgJson);
            return text.Contains("\"dev\":") || text.Contains("\"dev\" :");
        }
        catch
        {
            return false;
        }
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
        var pkgJson = Path.Combine(dir, "package.json");
        if (File.Exists(pkgJson))
        {
            var (devCmd, buildCmd) = ResolveNodeScripts(pkgJson);
            results.Add(new DiscoveredProject(name, ToolchainKind.Node, dir, devCmd, buildCmd));
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
