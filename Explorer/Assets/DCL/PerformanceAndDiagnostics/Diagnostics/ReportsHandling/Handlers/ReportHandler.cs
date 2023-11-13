using System;

namespace DCL.Diagnostics
{
    [Flags]
    public enum ReportHandler : byte
    {
        /// <summary>
        ///     Just a default value, should not be referenced
        /// </summary>
        None = 0,

        /// <summary>
        ///     Standard Unity Debug Log
        /// </summary>
        DebugLog = 1,

        /// <summary>
        ///     Sentry
        /// </summary>
        Sentry = 1 << 1,

        /// <summary>
        ///     All handlers
        /// </summary>
        All = DebugLog | Sentry,
    }
}
