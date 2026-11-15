using Omni.Core.Detection;

namespace Omni.Core.Tests;

public class WorkspaceDetectorTests
{
    [Fact]
    public void Detect_Finds_DotNet_Solution_In_Omni_Root()
    {
        var detector = new WorkspaceDetector();
        var currentDir = AppContext.BaseDirectory;
        
        // Traverse up to find omni folder
        var dir = new DirectoryInfo(currentDir);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "Omni.slnx")))
        {
            dir = dir.Parent;
        }

        Assert.NotNull(dir);
        var projects = detector.Detect(dir.FullName);

        Assert.NotEmpty(projects);
        Assert.Contains(projects, p => p.Kind == ToolchainKind.DotNet);
    }
}
