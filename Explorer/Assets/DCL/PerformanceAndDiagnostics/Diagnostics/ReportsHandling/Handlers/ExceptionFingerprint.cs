using Sentry;
using System;

namespace DCL.Diagnostics
{
    /// <summary>
    ///     Simplified representation of the exception for the report purpose only.
    ///     <list type="bullet">
    ///         <item> Use it with caution as the comparison is superficial</item>
    ///         <item> Should be applied manually, not automatically everywhere</item>
    ///         <item> Allocations are minimized to a single message</item>
    ///         <item> The real callstack is ignored</item>
    ///     </list>
    ///     <remarks>
    ///         <list type="bullet">
    ///             <item> There is no definitive built-in mechanism to compare exceptions </item>
    ///             <item> Doing it manually would involve a lot of allocations and boilerplate code </item>
    ///             <item> It would be very CPU intensive to do so </item>
    ///             <item> Comparing <see cref="SentryMessage" /> could be an option, but it doesn't have a built-in comparer either, and it requires huge allocations to parse an exception so it's better to avoid creating that object in advance</item>
    ///         </list>
    ///     </remarks>
    /// </summary>
    public readonly struct ExceptionFingerprint : IEquatable<ExceptionFingerprint>
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

        public readonly Type Type;
        public readonly Type? InnerType;
        public readonly string Message;
        public readonly CallStackFingerprint CallStack;

        public ExceptionFingerprint(Exception e, CallStackFingerprint? callStack = null)
        {
            CallStack = callStack ?? new CallStackFingerprint(string.Empty);
            Type = e.GetType();
            InnerType = e.InnerException?.GetType();
            Message = e.Message;
        }

        public bool Equals(ExceptionFingerprint other) =>
            Type.Equals(other.Type) && Equals(InnerType, other.InnerType) && Message == other.Message && CallStack.Equals(other.CallStack);

        public override bool Equals(object? obj) =>
            obj is ExceptionFingerprint other && Equals(other);

        public override int GetHashCode() =>
            HashCode.Combine(Type, InnerType, Message, CallStack);
    }
}
