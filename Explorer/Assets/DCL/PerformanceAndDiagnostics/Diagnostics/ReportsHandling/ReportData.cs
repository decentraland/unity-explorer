using System;

namespace DCL.Diagnostics
{
    /// <summary>
    ///     Contains additional data that is attached to the log report.
    ///     Consider extending it with everything needed for console, Sentry, etc.
    /// </summary>
    public struct ReportData : IEquatable<ReportData> // IEquatable is used by tests
    {
        public static readonly ReportData UNSPECIFIED = new (ReportCategory.UNSPECIFIED);

        public readonly string Category;
        public readonly ReportHint Hint;
        public readonly IReportsDebouncer? Debouncer;

        public SceneShortInfo SceneShortInfo;
        public uint? SceneTickNumber;

        public ReportData(string category, ReportHint hint = ReportHint.None,
            SceneShortInfo sceneShortInfo = default,
            uint? sceneTickNumber = null,
            IReportsDebouncer? debouncer = null)
        {
            Category = category;
            Hint = hint;
            SceneShortInfo = sceneShortInfo;
            SceneTickNumber = sceneTickNumber;
            Debouncer = debouncer;
        }

        public static implicit operator ReportData(string category) =>
            new (category);

        public ReportData WithSessionStatic() =>
            new (Category, ReportHint.SessionStatic | Hint, SceneShortInfo, SceneTickNumber);

        public bool Equals(ReportData other) =>
            Category == other.Category && Hint == other.Hint && Equals(Debouncer, other.Debouncer) && SceneShortInfo.Equals(other.SceneShortInfo) && SceneTickNumber == other.SceneTickNumber;

        public override bool Equals(object? obj) =>
            obj is ReportData other && Equals(other);

        public override int GetHashCode() =>
            HashCode.Combine(Category, (int)Hint, Debouncer, SceneShortInfo, SceneTickNumber);
    }
}
