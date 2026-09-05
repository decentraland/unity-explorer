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

        public StaticDebouncer() : this(ReportHandler.All) { }

        internal StaticDebouncer(ReportHandler appliedTo)
        {
            AppliedTo = appliedTo;
        }

        public ReportHandler AppliedTo { get; }

        public bool Debounce(ReportMessageFingerprint fingerprint) =>
            !fingerprints.Add(fingerprint);
    }

    /// <summary>
    ///     <inheritdoc cref="StaticDebouncer" />. Applies only to Sentry reports.
    /// </summary>
    [Singleton(SingletonGenerationBehavior.ALLOW_IMPLICIT_CONSTRUCTION)]
    public partial class SentryStaticDebouncer : StaticDebouncer
    {
        public SentryStaticDebouncer() : base(ReportHandler.Sentry) { }
    }
}
