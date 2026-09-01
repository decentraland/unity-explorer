namespace Utility
{
    /// <summary>
    ///     Raised when the analytics persistent queue reports the disk is full (SQLITE_FULL).
    ///     Declared here (not in DCL.Analytics) so UI-layer subscribers can reference it
    ///     without depending on the analytics assembly.
    /// </summary>
    public readonly struct AnalyticsDiskFullDetected { }
}
