using UnityEngine;

namespace DCL.BugReporting
{
    /// <summary>
    ///     Everything the user and the caller provide for one bug report. Machine context
    ///     (OS, GPU, versions) is gathered by <see cref="BugReportService" /> itself.
    /// </summary>
    public struct BugReportInput
    {
        public BugReportIssueType IssueType;
        public string Description;

        /// <summary>
        ///     Image the user attached, already encoded. It travels both to Sentry as the feedback
        ///     attachment and to the Intercom proxy, which hosts it and inlines it into the ticket.
        /// </summary>
        public byte[]? Image;

        /// <summary>Mime type of <see cref="Image" />, e.g. "image/jpeg".</summary>
        public string? ImageContentType;

        public string? ContactEmail;
        public string? UserName;
        public Vector2Int? Coordinates;

        /// <summary>Outcome of the startup hardware check; null when unknown.</summary>
        public bool? MeetsMinimumSpecs;

        /// <summary>Sdk version of the scene the player stands on; null when on none.</summary>
        public string? SceneSdkVersion;

        /// <summary>Version of the launcher that started the client; null when launched without one.</summary>
        public string? LauncherVersion;
    }
}
