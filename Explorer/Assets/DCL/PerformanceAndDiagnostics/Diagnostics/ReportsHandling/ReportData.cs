namespace DCL.Diagnostics
{
    /// <summary>
    ///     Contains additional data that is attached to the log report.
    ///     Consider extending it with everything needed for console, Sentry, etc.
    /// </summary>
    public struct ReportData
    {
        public static readonly ReportData UNSPECIFIED = new (ReportCategory.UNSPECIFIED);

        public readonly string Category;
        public readonly ReportHint Hint;

        public SceneShortInfo SceneShortInfo;

        public ReportData(string category, ReportHint hint = ReportHint.None, SceneShortInfo sceneShortInfo = default)
        {
            Category = category;
            Hint = hint;
            SceneShortInfo = sceneShortInfo;
        }

        public static implicit operator ReportData(string category) =>
            new (category);
    }
}
