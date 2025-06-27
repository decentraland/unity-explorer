using System.Collections.Generic;

namespace DCL.Diagnostics
{
    /// <summary>
    ///     Debounce messages based on the <see cref="ExceptionFingerprint" /> or the default message comparison, and a time-like characteristic.
    /// </summary>
    public abstract class TimingBasedDebouncer<TTiming> : IReportsDebouncer, IEqualityComparer<ExceptionFingerprint> where TTiming: struct
    {
        protected readonly Dictionary<ReportMessageFingerprint, TTiming> messages = new (50);

        public abstract ReportHandler AppliedTo { get; }

        public abstract bool Debounce(ReportMessageFingerprint fingerprint);

        public IReadOnlyDictionary<ReportMessageFingerprint, TTiming> Messages => messages;

        public bool Equals(ExceptionFingerprint x, ExceptionFingerprint y) =>
            x.Equals(y);

        public int GetHashCode(ExceptionFingerprint obj) =>
            obj.GetHashCode();
    }
}
