using System;

namespace DCL.Diagnostics
{
    /// <summary>
    ///     Fake call stack fingerprint.
    /// </summary>
    public readonly struct CallStackFingerprint : IEquatable<CallStackFingerprint>
    {
        public readonly string Fingerprint;

        public CallStackFingerprint(string fingerprint)
        {
            Fingerprint = fingerprint;
        }

        public bool Equals(CallStackFingerprint other) =>
            Fingerprint == other.Fingerprint;

        public override bool Equals(object? obj) =>
            obj is CallStackFingerprint other && Equals(other);

        public override int GetHashCode() =>
            Fingerprint.GetHashCode();
    }
}
