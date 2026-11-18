using System.Text.RegularExpressions;

namespace Omni.Core.Diagnostics;

public class FailureAnalyzer
{
    private static readonly Regex PortConflictKeywordRegex = new(
        @"(?:EADDRINUSE|address already in use|Failed to bind to address|Only one usage of each socket address|port.*already in use|listen tcp.*bind)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex PortNumberExtractRegex = new(
        @"(?:::|:|port\s+)(\d{2,5})",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex NodeModuleRegex = new(
        @"Cannot find module '([^']+)'",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex PythonModuleRegex = new(
        @"(?:ModuleNotFoundError|No module named) '([^']+)'",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex DotnetNugetRegex = new(
        @"error (?:NU\d+|MSB\d+):.*(?:package restore failed|Unable to resolve '([^']+)')",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex ConnectionRefusedRegex = new(
        @"(?:ECONNREFUSED|Connection refused|connect: connection refused|could not connect to server|dial tcp [^:]*:\d+: connect)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex CommandNotFoundWindowsRegex = new(
        @"'([^']+)' is not recognized as an internal or external command",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex CommandNotFoundUnixRegex = new(
        @"(?:command not found: ([^\s]+)|([^\s/]+): command not found|/bin/sh: line \d+: ([^\s]+): not found)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex CSharpCompilerRegex = new(
        @"error CS\d+: (.*)",
        RegexOptions.Compiled);

    private static readonly Regex SyntaxErrorRegex = new(
        @"(?:SyntaxError|ParseError): (.*)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex NextJsDuplicateRegex = new(
        @"(?:Another next dev server is already running|run taskkill /PID\s+(\d+)\s+/F)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex NextJsPidExtractRegex = new(
        @"(?:PID:\s+(\d+)|/PID\s+(\d+))",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex NpmMissingScriptRegex = new(
        @"npm error Missing script:\s*""([^""]+)""",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex EnvVarRegex = new(
        @"(?:KeyError: '([^']+)'|Environment variable '([^']+)' (?:is missing|not found)|Missing required environment variable:? ([^\s]+))",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static FailureDiagnosis Analyze(string serviceName, int exitCode, IReadOnlyList<string> lines)
    {
        var combinedText = string.Join("\n", lines);

        // 1. Check Port Collision
        if (NextJsDuplicateRegex.IsMatch(combinedText))
        {
            var pidMatch = NextJsPidExtractRegex.Match(combinedText);
            var pidStr = pidMatch.Success ? (pidMatch.Groups[1].Success ? pidMatch.Groups[1].Value : pidMatch.Groups[2].Value) : "unknown";

            return new FailureDiagnosis(
                serviceName,
                exitCode,
                DiagnosticCategory.PortCollision,
                $"Port Collision: Next.js dev server already active (PID: {pidStr})",
                $"Another Next.js development server is already running and bound to port 3000 (Process ID: {pidStr}).",
                $"Terminate the lingering instance with 'taskkill /PID {pidStr} /F' on Windows (or 'kill -9 {pidStr}' on Unix), or configure a different port.",
                pidStr
            );
        }

        if (PortConflictKeywordRegex.IsMatch(combinedText))
        {
            var portNumberMatch = PortNumberExtractRegex.Match(combinedText);
            var portStr = portNumberMatch.Success ? portNumberMatch.Groups[1].Value : "unknown";

            return new FailureDiagnosis(
                serviceName,
                exitCode,
                DiagnosticCategory.PortCollision,
                $"Port Collision: Port {portStr} is already occupied",
                $"Service attempted to listen on port {portStr}, but another running application or orphan process is already bound to it.",
                $"Terminate the process using port {portStr} (e.g. Stop-Process or taskkill on Windows, or lsof -i :{portStr}), or change the port in omni.yaml.",
                portStr
            );
        }

        // 2. Check Missing Dependencies (Node, Python, .NET)
        var nodeMatch = NodeModuleRegex.Match(combinedText);
        if (nodeMatch.Success)
        {
            var module = nodeMatch.Groups[1].Value;
            return new FailureDiagnosis(
                serviceName,
                exitCode,
                DiagnosticCategory.MissingDependency,
                $"Missing Node Module: '{module}'",
                $"The application could not find module '{module}'. The package dependencies have not been installed or were newly added.",
                $"Run 'npm install' or 'pnpm install' in the service directory to install dependencies.",
                module
            );
        }

        var pyMatch = PythonModuleRegex.Match(combinedText);
        if (pyMatch.Success)
        {
            var module = pyMatch.Groups[1].Value;
            return new FailureDiagnosis(
                serviceName,
                exitCode,
                DiagnosticCategory.MissingDependency,
                $"Missing Python Module: '{module}'",
                $"Python could not import '{module}'. The virtual environment might not be activated or the package is not installed.",
                $"Run 'pip install {module}' or install required packages with 'pip install -r requirements.txt'.",
                module
            );
        }

        var dotnetMatch = DotnetNugetRegex.Match(combinedText);
        if (dotnetMatch.Success)
        {
            return new FailureDiagnosis(
                serviceName,
                exitCode,
                DiagnosticCategory.MissingDependency,
                "NuGet Package Restore Failure",
                "One or more required NuGet packages could not be resolved or restored.",
                "Run 'dotnet restore' in the service directory to download and restore dependencies.",
                dotnetMatch.Value
            );
        }

        // 3. Check Connection Refused (DB, Redis, etc.)
        if (ConnectionRefusedRegex.IsMatch(combinedText))
        {
            return new FailureDiagnosis(
                serviceName,
                exitCode,
                DiagnosticCategory.ConnectionRefused,
                "Upstream Connection Refused",
                "The service failed to connect to an external resource (e.g., database, cache, or another local service).",
                "Ensure your backing services (PostgreSQL, Redis, RabbitMQ, Docker containers) are running and accessible on their expected host and port."
            );
        }

        // 4. Missing Runtime / Command not found
        var cmdWinMatch = CommandNotFoundWindowsRegex.Match(combinedText);
        if (cmdWinMatch.Success)
        {
            var cmd = cmdWinMatch.Groups[1].Value;
            return new FailureDiagnosis(
                serviceName,
                exitCode,
                DiagnosticCategory.MissingRuntime,
                $"Command Not Found: '{cmd}'",
                $"The executable '{cmd}' was not found in the system PATH.",
                $"Install {cmd} or check your PATH environment variables so it is available from your terminal.",
                cmd
            );
        }

        var cmdUnixMatch = CommandNotFoundUnixRegex.Match(combinedText);
        if (cmdUnixMatch.Success)
        {
            string cmd = "command";
            for (int i = 1; i < cmdUnixMatch.Groups.Count; i++)
            {
                if (!string.IsNullOrEmpty(cmdUnixMatch.Groups[i].Value))
                {
                    cmd = cmdUnixMatch.Groups[i].Value;
                    break;
                }
            }
            return new FailureDiagnosis(
                serviceName,
                exitCode,
                DiagnosticCategory.MissingRuntime,
                $"Command Not Found: '{cmd}'",
                $"The system could not locate '{cmd}' to execute the service command.",
                $"Install {cmd} and verify it is present in PATH.",
                cmd
            );
        }

        // 5. C# Compilation Error
        var csMatch = CSharpCompilerRegex.Match(combinedText);
        if (csMatch.Success)
        {
            return new FailureDiagnosis(
                serviceName,
                exitCode,
                DiagnosticCategory.BuildOrSyntaxError,
                "C# Compilation Error",
                $"Source code contains build errors: {csMatch.Groups[1].Value}",
                "Open the code file indicated above, correct the compilation syntax or type error, and re-run.",
                csMatch.Value
            );
        }

        // 6. Generic Syntax Error
        var syntaxMatch = SyntaxErrorRegex.Match(combinedText);
        if (syntaxMatch.Success)
        {
            return new FailureDiagnosis(
                serviceName,
                exitCode,
                DiagnosticCategory.BuildOrSyntaxError,
                "Syntax or Parsing Error",
                $"Syntax error encountered during parsing: {syntaxMatch.Groups[1].Value}",
                "Check the offending file and line syntax highlighted in the log above.",
                syntaxMatch.Value
            );
        }

        // 7. Environment Variable Missing
        var envMatch = EnvVarRegex.Match(combinedText);
        if (envMatch.Success)
        {
            string varName = "ENV_VAR";
            for (int i = 1; i < envMatch.Groups.Count; i++)
            {
                if (!string.IsNullOrEmpty(envMatch.Groups[i].Value))
                {
                    varName = envMatch.Groups[i].Value;
                    break;
                }
            }

            return new FailureDiagnosis(
                serviceName,
                exitCode,
                DiagnosticCategory.EnvironmentMissing,
                $"Missing Environment Variable: '{varName}'",
                $"The application expected an environment variable '{varName}' which is currently undefined.",
                $"Define '{varName}' in your .env file or under services.{serviceName}.env in omni.yaml.",
                varName
            );
        }

        // 8. Missing npm script check
        var missingScriptMatch = NpmMissingScriptRegex.Match(combinedText);
        if (missingScriptMatch.Success)
        {
            var scriptName = missingScriptMatch.Groups[1].Value;
            return new FailureDiagnosis(
                serviceName,
                exitCode,
                DiagnosticCategory.MissingDependency,
                $"Missing npm script: '{scriptName}'",
                $"The package.json for service '{serviceName}' does not define a script named '{scriptName}'.",
                $"Add a '{scriptName}' script to package.json, or configure a custom dev command under services.{serviceName}.command in omni.yaml.",
                scriptName
            );
        }

        // 9. Fallback Unknown
        var lastMeaningfulLine = lines
            .Select(l => l.Trim())
            .LastOrDefault(l => !string.IsNullOrWhiteSpace(l)) 
            ?? "Service exited unexpectedly without standard output.";

        return new FailureDiagnosis(
            serviceName,
            exitCode,
            DiagnosticCategory.Unknown,
            $"Process Exited with Code {exitCode}",
            $"The service terminated with non-zero exit code ({exitCode}). Last captured output:\n  \"{lastMeaningfulLine}\"",
            "Review the service startup arguments, working directory, and logs above."
        );
    }
}

