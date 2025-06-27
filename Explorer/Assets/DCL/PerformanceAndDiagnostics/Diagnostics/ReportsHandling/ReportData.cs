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
        public readonly ReportDebounce Debounce;

        public SceneShortInfo SceneShortInfo;
        public uint? SceneTickNumber;

        public ReportData(string category, ReportDebounce debounce = default,
            SceneShortInfo sceneShortInfo = default,
            uint? sceneTickNumber = null)
        {
            Category = category;
            Debounce = debounce;
            SceneShortInfo = sceneShortInfo;
            SceneTickNumber = sceneTickNumber;
        }

        public static implicit operator ReportData(string category) =>
            new (category);

        public ReportData WithStaticDebounce() =>
            new (Category, ReportDebounce.AssemblyStatic, SceneShortInfo, SceneTickNumber);

        public bool Equals(ReportData other) =>
            Category == other.Category && Debounce.Equals(other.Debounce) && SceneShortInfo.Equals(other.SceneShortInfo) && SceneTickNumber == other.SceneTickNumber;

        public override bool Equals(object? obj) =>
            obj is ReportData other && Equals(other);

        public override int GetHashCode() =>
            HashCode.Combine(Category, Debounce, SceneShortInfo, SceneTickNumber);
    }
}
