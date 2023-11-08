using System;

namespace DCL.Diagnostics
{
    [Flags]
    public enum ReportHint : byte
    {
        /// <summary>
        ///     No hints
        /// </summary>
        None = 0,

        /// <summary>
        ///     Report is the same every time and will not change within the assembly
        /// </summary>
        AssemblyStatic = 1,

        /// <summary>
        ///     Report is the same every time and will not change within the session
        /// </summary>
        SessionStatic = 1 << 1,
    }
}
