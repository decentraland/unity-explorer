namespace DCL.BugReporting.UI
{
    /// <summary>Input for showing the bug report form.</summary>
    public readonly struct BugReportParams
    {
        /// <summary>Optional initial content of the description field.</summary>
        public readonly string? PrefilledDescription;

        /// <summary>Optional initial selection of the issue type dropdown.</summary>
        public readonly BugReportIssueType? PrefilledIssueType;

        /// <summary>
        ///     Raises the form above Overlay-layer views. Set it when the entry point lives on one
        ///     (e.g. the loading screen), where the popup ordering would draw the form behind it.
        /// </summary>
        public readonly bool ShowAboveOverlays;

        public BugReportParams(string? prefilledDescription = null, BugReportIssueType? prefilledIssueType = null, bool showAboveOverlays = false)
        {
            PrefilledDescription = prefilledDescription;
            PrefilledIssueType = prefilledIssueType;
            ShowAboveOverlays = showAboveOverlays;
        }
    }
}
