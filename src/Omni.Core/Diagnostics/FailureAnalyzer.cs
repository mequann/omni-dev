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

    private static readonly Regex DbUniqueConstraintRegex = new(
        @"(?:duplicate key value violates unique constraint ""(?<constraint>[^""]+)""|Unique constraint failed on the fields?: \((?<fields>[^)]+)\)|UNIQUE constraint failed: (?<fields>[^\r\n]+))",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex DbUniqueDetailRegex = new(
        @"DETAIL:\s*Key \((?<field>[^)]+)\)=\((?<val>[^)]+)\) already exists",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex DbExclusionConstraintRegex = new(
        @"conflicting key value violates exclusion constraint ""(?<constraint>[^""]+)""",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex DbForeignKeyRegex = new(
        @"(?:violates foreign key constraint ""(?<constraint>[^""]+)""|FOREIGN KEY constraint failed)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static (string host, string port) ExtractConnectionRefusedTarget(string text)
    {
        var m1 = Regex.Match(text, @"(?:connect\s+ECONNREFUSED|refused|dial tcp)\s+(?:http:\/\/)?([0-9a-zA-Z\.\-]+):(\d{2,5})", RegexOptions.IgnoreCase);
        if (m1.Success)
            return (m1.Groups[1].Value, m1.Groups[2].Value);

        var m2 = Regex.Match(text, @"address:\s*'([^']+)',\s*port:\s*(\d{2,5})", RegexOptions.IgnoreCase);
        if (m2.Success)
            return (m2.Groups[1].Value, m2.Groups[2].Value);

        var m3 = Regex.Match(text, @"(?:http:\/\/)([0-9a-zA-Z\.\-]+):(\d{2,5})", RegexOptions.IgnoreCase);
        if (m3.Success)
            return (m3.Groups[1].Value, m3.Groups[2].Value);

        return ("unknown host", "unknown port");
    }

    public static FailureDiagnosis? TryAnalyzeLive(string serviceName, string line, IReadOnlyList<string> recentLines)
    {
        var context = line;
        if (recentLines != null && recentLines.Count > 0)
        {
            context = string.Join("\n", recentLines.TakeLast(6)) + "\n" + line;
        }

        // Live Connection Refused (e.g. Next.js proxy ECONNREFUSED)
        if (ConnectionRefusedRegex.IsMatch(context))
        {
            var (host, port) = ExtractConnectionRefusedTarget(context);
            string targetDesc = port != "unknown port" ? $"{host}:{port}" : host;

            return new FailureDiagnosis(
                serviceName,
                0,
                DiagnosticCategory.ConnectionRefused,
                $"Upstream Connection Refused: Unable to reach {targetDesc}",
                $"Service '{serviceName}' attempted to make a network call or proxy requests to {targetDesc}, but nothing is listening on port {port}.",
                $"Check if the backend service ('api') is still compiling/starting up, or verify that the backend's port matches {port} in your environment or proxy settings.",
                targetDesc,
                IsLive: true
            );
        }

        // Live Port Collision (dev server tried to bind port during runtime)
        if (PortConflictKeywordRegex.IsMatch(context))
        {
            var portNumberMatch = PortNumberExtractRegex.Match(context);
            var portStr = portNumberMatch.Success ? portNumberMatch.Groups[1].Value : "unknown";

            return new FailureDiagnosis(
                serviceName,
                0,
                DiagnosticCategory.PortCollision,
                $"Port Collision: Port {portStr} is already occupied",
                $"Service attempted to listen on port {portStr}, but another running application or orphan process is already bound to it.",
                $"Terminate the process using port {portStr} or change the port in omni.yaml.",
                portStr,
                IsLive: true
            );
        }

        // Live Missing Dependency
        var nodeMatch = NodeModuleRegex.Match(context);
        if (nodeMatch.Success)
        {
            var module = nodeMatch.Groups[1].Value;
            return new FailureDiagnosis(
                serviceName,
                0,
                DiagnosticCategory.MissingDependency,
                $"Missing Node Module: '{module}'",
                $"The application could not find module '{module}'. The package dependencies have not been installed or were newly added.",
                $"Run 'npm install' or 'pnpm install' in the service directory to install dependencies.",
                module,
                IsLive: true
            );
        }

        // Live Database Constraint Violations
        var dbDiag = TryAnalyzeDatabase(serviceName, 0, context, isLive: true);
        if (dbDiag != null)
        {
            return dbDiag;
        }

        return null;
    }

    private static FailureDiagnosis? TryAnalyzeDatabase(string serviceName, int exitCode, string text, bool isLive)
    {
        // Unique Constraint Violation
        var uniqueMatch = DbUniqueConstraintRegex.Match(text);
        if (uniqueMatch.Success)
        {
            var constraintName = uniqueMatch.Groups["constraint"].Success ? uniqueMatch.Groups["constraint"].Value : uniqueMatch.Groups["fields"].Value;
            var detailMatch = DbUniqueDetailRegex.Match(text);
            string detailText = detailMatch.Success 
                ? $"Field '{detailMatch.Groups["field"].Value}' with value '{detailMatch.Groups["val"].Value}'" 
                : $"Constraint '{constraintName}'";

            return new FailureDiagnosis(
                serviceName,
                exitCode,
                DiagnosticCategory.DatabaseError,
                $"Database Unique Constraint Violation: duplicate key on {constraintName}",
                $"An insert or update attempted to write duplicate values ({detailText}), violating unique database constraint '{constraintName}'.",
                "Ensure unique values before writing, use 'upsert' instead of direct 'insert' in seed scripts, or handle unique constraint violations in API controllers.",
                constraintName,
                isLive
            );
        }

        // Exclusion Constraint Violation (e.g. overlapping timetable or leave intervals)
        var exclMatch = DbExclusionConstraintRegex.Match(text);
        if (exclMatch.Success)
        {
            var constraintName = exclMatch.Groups["constraint"].Value;
            return new FailureDiagnosis(
                serviceName,
                exitCode,
                DiagnosticCategory.DatabaseError,
                $"Database Exclusion Constraint Violation: overlapping range in {constraintName}",
                $"A row with overlapping schedule, date range, or interval was inserted, violating exclusion constraint '{constraintName}'.",
                "Check for overlapping date or time ranges for the same entity before inserting or updating records.",
                constraintName,
                isLive
            );
        }

        // Foreign Key Constraint Violation
        var fkMatch = DbForeignKeyRegex.Match(text);
        if (fkMatch.Success)
        {
            var constraintName = fkMatch.Groups["constraint"].Success ? fkMatch.Groups["constraint"].Value : "foreign_key";
            return new FailureDiagnosis(
                serviceName,
                exitCode,
                DiagnosticCategory.DatabaseError,
                $"Database Foreign Key Violation: {constraintName}",
                $"An operation failed because a referenced parent record does not exist, or a parent record cannot be deleted while child records still reference it.",
                "Ensure parent records exist before inserting, or delete/reassign dependent child records (or enable cascade delete: onDelete: Cascade).",
                constraintName,
                isLive
            );
        }

        return null;
    }

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

        // 3. Check Connection Refused (DB, Redis, API proxy, etc.)
        if (ConnectionRefusedRegex.IsMatch(combinedText))
        {
            var (host, port) = ExtractConnectionRefusedTarget(combinedText);
            string targetDesc = port != "unknown port" ? $"{host}:{port}" : host;

            return new FailureDiagnosis(
                serviceName,
                exitCode,
                DiagnosticCategory.ConnectionRefused,
                $"Upstream Connection Refused: Unable to reach {targetDesc}",
                $"The service failed to connect to {targetDesc}. No active process is listening on port {port}.",
                $"Ensure your backing service, backend API, or database is running and accessible on port {port}.",
                targetDesc
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

        // 9. Database Constraint Violation (PostgreSQL, Prisma, SQLite)
        var dbDiag = TryAnalyzeDatabase(serviceName, exitCode, combinedText, isLive: false);
        if (dbDiag != null)
        {
            return dbDiag;
        }

        // 10. Fallback Unknown
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

