using CodeLess.Attributes;
using System.Collections.Generic;

namespace DCL.Diagnostics
{
    /// <summary>
    ///     Allows only one message to be logged, regardless of the number of frames or time that passes.
    /// </summary>
    [Singleton(SingletonGenerationBehavior.ALLOW_IMPLICIT_CONSTRUCTION)]
    public partial class StaticDebouncer : IReportsDebouncer
    {
        private readonly HashSet<ReportMessageFingerprint> fingerprints = new ();

        public ReportHandler AppliedTo => ReportHandler.All;

        public bool Debounce(ReportMessageFingerprint fingerprint) =>
            !fingerprints.Add(fingerprint);
    }
}
