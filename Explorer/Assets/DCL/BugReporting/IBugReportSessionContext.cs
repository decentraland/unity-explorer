namespace DCL.BugReporting
{
    /// <summary>Session and world context a bug report carries, resolved at submit time.</summary>
    public interface IBugReportSessionContext
    {
        bool? MeetsMinimumSpecs { get; }
        string? SceneSdkVersion { get; }
        string? LauncherVersion { get; }
    }
}
