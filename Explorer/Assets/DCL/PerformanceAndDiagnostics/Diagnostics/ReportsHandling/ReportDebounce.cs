using System;

namespace DCL.Diagnostics
{
    public struct ReportDebounce : IEquatable<ReportDebounce>
    {
        public ReportDebounce WithCallstack(string callStackHint) =>
            new (Debouncer!, new CallStackFingerprint(callStackHint));

        /// <summary>
        ///     No hints, is equal to default(ReportDebounce)
        /// </summary>
        public static readonly ReportDebounce NONE = new ();

        /// <summary>
        ///     Report is the same every time and will not change within the assembly
        /// </summary>
        public static ReportDebounce AssemblyStatic => new (StaticDebouncer.Instance);

        public readonly IReportsDebouncer? Debouncer;
        public readonly CallStackFingerprint? CallStackHint;

        public ReportDebounce(IReportsDebouncer debouncer, CallStackFingerprint? callStackHint = null)
        {
            Debouncer = debouncer;
            CallStackHint = callStackHint;
        }

        public bool Equals(ReportDebounce other) =>
            Equals(Debouncer, other.Debouncer) && Nullable.Equals(CallStackHint, other.CallStackHint);

        public override bool Equals(object? obj) =>
            obj is ReportDebounce other && Equals(other);

        public override int GetHashCode() =>
            HashCode.Combine(Debouncer, CallStackHint);
    }
}
