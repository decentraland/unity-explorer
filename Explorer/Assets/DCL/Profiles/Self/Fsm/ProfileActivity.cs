using REnum;
using System;
using System.Runtime.CompilerServices;

namespace DCL.Profiles.Self
{
    public readonly struct Deploying : IEquatable<Deploying>
    {
        public readonly Profile Pending;

        /// <summary>Knowledge at the moment the deploy started; the model reverts to it when the deploy fails.</summary>
        public readonly ProfileKnowledge Before;

        /// <summary>Deploy requests waiting on this deploy, including those of the deploys it superseded.</summary>
        public readonly RequestIds Requests;

        public Deploying(Profile pending, ProfileKnowledge before)
            : this(pending, before, default) { }

        public Deploying(Profile pending, ProfileKnowledge before, RequestIds requests)
        {
            Pending = pending;
            Before = before;
            Requests = requests;
        }

        public bool Equals(Deploying other) =>
            ReferenceEquals(Pending, other.Pending) && Before.Equals(other.Before) && Requests.Equals(other.Requests);

        public override bool Equals(object? obj) =>
            obj is Deploying other && Equals(other);

        public override int GetHashCode() =>
            HashCode.Combine(RuntimeHelpers.GetHashCode(Pending), Before, Requests);

        public override string ToString() =>
            $"pending v{Pending.Version} before {Before} {Requests}";
    }

    /// <summary>
    ///     What the self-profile runtime is doing right now. At most one activity is in flight at a time.
    /// </summary>
    [REnum(EnumUnderlyingType.Byte)]
    [REnumFieldEmpty("Idle")]
    [REnumFieldEmpty("Fetching")]
    [REnumField(typeof(Deploying))]
    public readonly partial struct ProfileActivity { }
}
