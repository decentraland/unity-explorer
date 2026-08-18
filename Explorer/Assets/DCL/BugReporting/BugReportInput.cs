using UnityEngine;

namespace DCL.BugReporting
{
    /// <summary>Everything the user and the caller provide for one bug report; machine context is gathered by <see cref="BugReportService" />.</summary>
    public struct BugReportInput
    {
        public BugReportIssueType IssueType;
        public string Description;
        public byte[]? Image;
        public string? ImageContentType;
        public string? ContactEmail;
        public string? UserName;
        public Vector2Int? Coordinates;
        public bool? MeetsMinimumSpecs;
        public string? SceneSdkVersion;
        public string? LauncherVersion;
    }
}
