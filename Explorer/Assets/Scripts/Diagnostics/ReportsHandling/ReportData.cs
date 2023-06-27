namespace Diagnostics.ReportsHandling
{
    /// <summary>
    ///     Contains additional data that is attached to the log report.
    ///     Consider extending it with everything needed for console, Sentry, etc.
    /// </summary>
    public readonly struct ReportData
    {
        public static readonly ReportData UNSPECIFIED = new (ReportCategory.UNSPECIFIED);

        public readonly string Category;
        public readonly ReportHint Hint;

        public ReportData(string category, ReportHint hint = ReportHint.None)
        {
            Category = category;
            Hint = hint;
        }
    }
}
