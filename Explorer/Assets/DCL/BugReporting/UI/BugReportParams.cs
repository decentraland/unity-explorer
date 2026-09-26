namespace DCL.BugReporting.UI
{
    public readonly struct BugReportParams
    {
        public readonly string? PrefilledDescription;
        public readonly BugReportIssueType? PrefilledIssueType;

        /// <summary>Set it when the entry point lives on an Overlay-layer view, where the popup ordering would draw the form behind it.</summary>
        public readonly bool ShowAboveOverlays;

        public BugReportParams(string? prefilledDescription = null, BugReportIssueType? prefilledIssueType = null, bool showAboveOverlays = false)
        {
            PrefilledDescription = prefilledDescription;
            PrefilledIssueType = prefilledIssueType;
            ShowAboveOverlays = showAboveOverlays;
        }
    }
}
