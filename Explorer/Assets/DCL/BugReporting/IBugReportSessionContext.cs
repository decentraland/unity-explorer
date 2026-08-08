namespace DCL.BugReporting
{
    /// <summary>
    ///     Session and world context a bug report carries, resolved at submit time by the
    ///     composition root, where the sources live.
    /// </summary>
    public interface IBugReportSessionContext
    {
        /// <summary>Outcome of the startup hardware check; null while it has not run.</summary>
        bool? MeetsMinimumSpecs { get; }

        /// <summary>Sdk version of the scene the player stands on, e.g. "7.5.6"; null when on none.</summary>
        string? SceneSdkVersion { get; }

        /// <summary>Version the launcher passed on the command line; null when launched without it.</summary>
        string? LauncherVersion { get; }
    }
}
