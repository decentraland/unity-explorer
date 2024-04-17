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
        public uint? SceneTickNumber;

        public ReportData(string category, ReportHint hint = ReportHint.None, SceneShortInfo sceneShortInfo = default, uint? sceneTickNumber = null)
        {
            Category = category;
            Hint = hint;
            SceneShortInfo = sceneShortInfo;
            SceneTickNumber = sceneTickNumber;
        }

        public static implicit operator ReportData(string category) =>
            new (category);
    }
}
