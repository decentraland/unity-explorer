namespace DCL.BugReporting.UI
{
    /// <summary>Input for showing the bug report form.</summary>
    public readonly struct BugReportParams
    {
        /// <summary>Optional initial content of the description field.</summary>
        public readonly string? PrefilledDescription;

        public BugReportParams(string? prefilledDescription = null)
        {
            PrefilledDescription = prefilledDescription;
        }
    }
}
