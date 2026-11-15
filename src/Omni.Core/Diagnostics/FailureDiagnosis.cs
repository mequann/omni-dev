namespace Omni.Core.Diagnostics;

public enum DiagnosticCategory
{
    PortCollision,
    MissingDependency,
    ConnectionRefused,
    MissingRuntime,
    BuildOrSyntaxError,
    EnvironmentMissing,
    Unknown
}

public record FailureDiagnosis(
    string ServiceName,
    int ExitCode,
    DiagnosticCategory Category,
    string Summary,
    string WhyItHappened,
    string SuggestedRemediation,
    string? OffendingDetail = null
);

