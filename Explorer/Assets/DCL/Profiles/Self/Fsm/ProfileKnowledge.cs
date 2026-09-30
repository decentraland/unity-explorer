using REnum;
using System;

namespace DCL.Profiles.Self
{
    public enum FailureKind : byte
    {
        /// <summary>Transport error, timeout or a non-404 status. A later read may succeed.</summary>
        Transient,

        /// <summary>The catalyst returned a payload the converter rejected. Re-reading yields the same payload.</summary>
        Malformed,
    }

    public readonly struct ProfileFailure
    {
        public readonly FailureKind Kind;
        public readonly Exception Exception;

        public ProfileFailure(FailureKind kind, Exception exception)
        {
            Kind = kind;
            Exception = exception;
        }

        public override string ToString() =>
            $"{Kind} {Exception.GetType().Name}: {Exception.Message}";
    }

    /// <summary>
    ///     What is known about the self profile of the current address. It is independent of any activity in flight:
    ///     a fetch or a deploy never clears it, so the last trusted profile stays readable throughout.
    ///     <c>Unknown</c> = nothing fetched yet; <c>Known</c> = last trusted profile; <c>Missing</c> = the catalyst answered 404;
    ///     <c>Failed</c> = the last read did not produce a profile.
    /// </summary>
    [REnum(EnumUnderlyingType.Byte)]
    [REnumFieldEmpty("Unknown")]
    [REnumField(typeof(Profile), "Known")]
    [REnumFieldEmpty("Missing")]
    [REnumField(typeof(ProfileFailure), "Failed")]
    public readonly partial struct ProfileKnowledge { }
}
