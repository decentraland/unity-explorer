using System.Collections.Generic;
using UnityEngine;

namespace DCL.BugReporting
{
    /// <summary>Everything the user and the caller provide for one bug report; machine context is gathered by <see cref="BugReportService" />.</summary>
    public struct BugReportInput
    {
        public BugReportIssueType IssueType;
        public string Description;

        /// <summary>In the order the user attached them; null or empty when there are none.</summary>
        public IReadOnlyList<EvidenceImage>? Images;

        public string? ContactEmail;
        public string? UserName;
        public Vector2Int? Coordinates;
        public bool? MeetsMinimumSpecs;
        public string? SceneSdkVersion;
        public string? LauncherVersion;
    }
}
