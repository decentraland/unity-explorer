namespace DCL.BugReporting.UI
{
    /// <summary>Input for showing the bug report form.</summary>
    public readonly struct BugReportParams
    {
        /// <summary>Optional initial content of the description field.</summary>
        public readonly string? PrefilledDescription;

        /// <summary>Optional initial selection of the issue type dropdown.</summary>
        public readonly BugReportIssueType? PrefilledIssueType;

        public BugReportParams(string? prefilledDescription = null, BugReportIssueType? prefilledIssueType = null)
        {
            PrefilledDescription = prefilledDescription;
            PrefilledIssueType = prefilledIssueType;
        }
    }
}
