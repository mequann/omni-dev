using Omni.Core.DevRunner;

namespace Omni.Core.Detection;

public enum ToolchainKind
{
    DotNet,
    Node,
    Python,
    DockerCompose,
    Rust,
    Go
}

public record DiscoveredProject(
    string Name,
    ToolchainKind Kind,
    string Path,
    string RecommendedDevCommand,
    string RecommendedDevArgs
);

public class WorkspaceDetector
{
    private static readonly string[] IgnoredDirectories = { ".git", "node_modules", "bin", "obj", ".vs", ".idea", "dist", "build", ".venv" };

    public List<DiscoveredProject> Detect(string rootDirectory)
    {
        var results = new List<DiscoveredProject>();

        if (!Directory.Exists(rootDirectory))
            return results;

        // 1. Check for root or subfolder .sln / .slnx or .csproj
        var slnFiles = Directory.EnumerateFiles(rootDirectory, "*.sln*", SearchOption.TopDirectoryOnly).ToList();
        if (slnFiles.Any())
        {
            results.Add(new DiscoveredProject(
                "dotnet-backend",
                ToolchainKind.DotNet,
                rootDirectory,
                "dotnet",
                "watch run"
            ));
        }

        // 2. Scan immediate and 1-level deep subdirectories
        var subDirs = Directory.GetDirectories(rootDirectory);
        foreach (var dir in subDirs)
        {
            var dirName = Path.GetFileName(dir);
            if (IgnoredDirectories.Contains(dirName))
                continue;

            // Check for Node
            var packageJson = Path.Combine(dir, "package.json");
            if (File.Exists(packageJson))
            {
                results.Add(new DiscoveredProject(
                    dirName,
                    ToolchainKind.Node,
                    dir,
                    "npm",
                    "run dev"
                ));
            }

            // Check for .csproj if no solution was detected at root
            if (!slnFiles.Any())
            {
                var csproj = Directory.EnumerateFiles(dir, "*.csproj", SearchOption.TopDirectoryOnly).FirstOrDefault();
                if (csproj != null)
                {
                    results.Add(new DiscoveredProject(
                        dirName,
                        ToolchainKind.DotNet,
                        dir,
                        "dotnet",
                        "watch run"
                    ));
                }
            }

            // Check for Python
            if (File.Exists(Path.Combine(dir, "pyproject.toml")) || File.Exists(Path.Combine(dir, "requirements.txt")))
            {
                results.Add(new DiscoveredProject(
                    dirName,
                    ToolchainKind.Python,
                    dir,
                    "python",
                    "-m uvicorn main:app --reload"
                ));
            }
        }

        // 3. Check for Docker Compose
        var dockerCompose = Directory.EnumerateFiles(rootDirectory, "*compose*.y*ml", SearchOption.TopDirectoryOnly).FirstOrDefault();
        if (dockerCompose != null)
        {
            results.Add(new DiscoveredProject(
                "docker-services",
                ToolchainKind.DockerCompose,
                rootDirectory,
                "docker",
                "compose up"
            ));
        }

        return results;
    }
}
