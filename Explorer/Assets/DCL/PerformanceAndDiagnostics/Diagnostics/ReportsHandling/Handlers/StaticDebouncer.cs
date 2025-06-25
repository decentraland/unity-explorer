using CodeLess.Attributes;
using System.Collections.Generic;
using UnityEngine;

namespace DCL.Diagnostics
{
    /// <summary>
    ///     Allows only one message to be logged, regardless of the number of frames or time that passes.
    /// </summary>
    [Singleton]
    public partial class StaticDebouncer : IReportsDebouncer
    {
        private readonly HashSet<ReportMessageFingerprint> fingerprints = new ();

        public ReportHandler AppliedTo => ReportHandler.All;

        public bool Debounce(ReportMessageFingerprint fingerprint, ReportData reportData, LogType log) =>
            !fingerprints.Add(fingerprint);
    }
}
