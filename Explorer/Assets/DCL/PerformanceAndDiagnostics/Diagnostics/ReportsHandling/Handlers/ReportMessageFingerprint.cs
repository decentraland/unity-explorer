using System;

namespace DCL.Diagnostics
{
    /// <summary>
    ///     Represents a string message or the exception fingerprint
    /// </summary>
    public readonly struct ReportMessageFingerprint : IEquatable<ReportMessageFingerprint>
    {
        private readonly string? message;
        private readonly ExceptionFingerprint? exceptionFingerprint;

        public ReportMessageFingerprint(string message)
        {
            this.message = message;
            exceptionFingerprint = null;
        }

        public ReportMessageFingerprint(ExceptionFingerprint exceptionFingerprint)
        {
            this.exceptionFingerprint = exceptionFingerprint;
            message = null;
        }

        public static implicit operator ReportMessageFingerprint(Exception e) =>
            new (new ExceptionFingerprint(e, null));

        public bool Equals(ReportMessageFingerprint other) =>
            message == other.message && Nullable.Equals(exceptionFingerprint, other.exceptionFingerprint);

        public override bool Equals(object? obj) =>
            obj is ReportMessageFingerprint other && Equals(other);

        public override int GetHashCode() =>
            HashCode.Combine(message, exceptionFingerprint);
    }
}
