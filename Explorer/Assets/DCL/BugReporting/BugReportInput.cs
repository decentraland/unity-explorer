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
        ///     Image the user attached, already encoded. It travels to Sentry only: the proxy
        ///     defines no upload envelope for it yet.
        /// </summary>
        public byte[]? Image;

        /// <summary>Mime type of <see cref="Image" />, e.g. "image/jpeg".</summary>
        public string? ImageContentType;

        /// <summary>Whether the user consented to sharing the client log.</summary>
        public bool ShareLogs;

        public string? ContactEmail;
        public string? UserName;
        public Vector2Int? Coordinates;
    }
}
