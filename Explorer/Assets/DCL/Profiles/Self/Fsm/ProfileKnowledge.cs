using REnum;
using System;
using System.Runtime.CompilerServices;
using Utility.Times;

namespace DCL.Profiles.Self
{
    public readonly struct ProfileFailure : IEquatable<ProfileFailure>
    {
        public const long MIN_BACK_OFF_MS = 2000;

        public const long MAX_BACK_OFF_MS = 60000;

        public readonly Exception Exception;

        public readonly UnixTimeMs FailedAt;

        /// <summary>Fetches failed in a row, this one included.</summary>
        public readonly int Streak;

        public ProfileFailure(Exception exception, UnixTimeMs failedAt, int streak = 1)
        {
            Exception = exception;
            FailedAt = failedAt;
            Streak = streak;
        }

        public long CalculateBackOffMs()
        {
            long backOff = MIN_BACK_OFF_MS;

            for (int i = 1; i < Streak && backOff < MAX_BACK_OFF_MS; i++)
                backOff *= 2;

            return Math.Min(backOff, MAX_BACK_OFF_MS);
        }

        /// <summary>The magnitude is compared, so a wall clock set back past the window ends it instead of stalling it.</summary>
        public bool IsBackingOffAt(UnixTimeMs now) =>
            Math.Abs(now - FailedAt) < CalculateBackOffMs();

        public ProfileFailure Following(in ProfileFailure previous) =>
            new (Exception, FailedAt, previous.Streak + 1);

        public bool Equals(ProfileFailure other) =>
            ReferenceEquals(Exception, other.Exception) && FailedAt == other.FailedAt && Streak == other.Streak;

        public override bool Equals(object? obj) =>
            obj is ProfileFailure other && Equals(other);

        public override int GetHashCode() =>
            HashCode.Combine(RuntimeHelpers.GetHashCode(Exception), FailedAt, Streak);

        public override string ToString() =>
            $"{Exception.GetType().Name}: {Exception.Message} (failure {Streak} at {FailedAt}, back-off {CalculateBackOffMs()} ms)";
    }

    /// <summary>
    ///     What is known about the self profile of the current address, independent of any activity in flight.
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
