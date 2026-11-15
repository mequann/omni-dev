using Omni.Core.Diagnostics;
using Xunit;

namespace Omni.Core.Tests;

public class FailureAnalyzerTests
{
    [Fact]
    public void Diagnoses_Port_Collision_EADDRINUSE()
    {
        var lines = new[]
        {
            "Starting frontend server on port 3000...",
            "Error: listen EADDRINUSE: address already in use :::3000",
            "    at Server.setupListenHandle [as _listen2] (net.js:1317:16)"
        };

        var diagnosis = FailureAnalyzer.Analyze("frontend", 1, lines);

        Assert.Equal(DiagnosticCategory.PortCollision, diagnosis.Category);
        Assert.Equal("3000", diagnosis.OffendingDetail);
        Assert.Contains("already occupied", diagnosis.Summary);
    }

    [Fact]
    public void Diagnoses_Missing_Node_Module()
    {
        var lines = new[]
        {
            "node:internal/modules/cjs/loader:1143",
            "Error: Cannot find module 'express'",
            "Require stack:",
            "- /app/server.js"
        };

        var diagnosis = FailureAnalyzer.Analyze("backend-node", 1, lines);

        Assert.Equal(DiagnosticCategory.MissingDependency, diagnosis.Category);
        Assert.Equal("express", diagnosis.OffendingDetail);
        Assert.Contains("npm install", diagnosis.SuggestedRemediation);
    }

    [Fact]
    public void Diagnoses_Missing_Python_Module()
    {
        var lines = new[]
        {
            "Traceback (most recent call last):",
            "  File \"app.py\", line 2, in <module>",
            "    import uvicorn",
            "ModuleNotFoundError: No module named 'uvicorn'"
        };

        var diagnosis = FailureAnalyzer.Analyze("ml-service", 1, lines);

        Assert.Equal(DiagnosticCategory.MissingDependency, diagnosis.Category);
        Assert.Equal("uvicorn", diagnosis.OffendingDetail);
        Assert.Contains("pip install", diagnosis.SuggestedRemediation);
    }

    [Fact]
    public void Diagnoses_Connection_Refused()
    {
        var lines = new[]
        {
            "Connecting to postgres at localhost:5432...",
            "Npgsql.NpgsqlException: Connection refused (127.0.0.1:5432)",
            "System.Net.Sockets.SocketException: No connection could be made because the target machine actively refused it."
        };

        var diagnosis = FailureAnalyzer.Analyze("api", 1, lines);

        Assert.Equal(DiagnosticCategory.ConnectionRefused, diagnosis.Category);
        Assert.Contains("Connection Refused", diagnosis.Summary);
    }

    [Fact]
    public void Diagnoses_Missing_CLI_Runtime()
    {
        var lines = new[]
        {
            "'cargo' is not recognized as an internal or external command, operable program or batch file."
        };

        var diagnosis = FailureAnalyzer.Analyze("rust-worker", 1, lines);

        Assert.Equal(DiagnosticCategory.MissingRuntime, diagnosis.Category);
        Assert.Equal("cargo", diagnosis.OffendingDetail);
    }
}
