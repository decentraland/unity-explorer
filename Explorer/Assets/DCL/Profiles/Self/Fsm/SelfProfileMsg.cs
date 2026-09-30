using REnum;
using System;

namespace DCL.Profiles.Self
{
    public readonly struct FetchSucceeded
    {
        public readonly UserId Address;
        public readonly Profile Profile;

        public FetchSucceeded(UserId address, Profile profile)
        {
            Address = address;
            Profile = profile;
        }

        public override string ToString() =>
            $"{Address.Value} v{Profile.Version}";
    }

    public readonly struct FetchFailed
    {
        public readonly UserId Address;
        public readonly ProfileFailure Failure;

        public FetchFailed(UserId address, ProfileFailure failure)
        {
            Address = address;
            Failure = failure;
        }

        public override string ToString() =>
            $"{Address.Value} {Failure}";
    }

    public readonly struct DeploySucceeded
    {
        public readonly UserId Address;

        /// <summary>The profile as re-read from the catalyst after the deploy, not the one that was sent.</summary>
        public readonly Profile Profile;

        public DeploySucceeded(UserId address, Profile profile)
        {
            Address = address;
            Profile = profile;
        }

        public override string ToString() =>
            $"{Address.Value} v{Profile.Version}";
    }

    public readonly struct DeployFailed
    {
        public readonly UserId Address;
        public readonly Exception Exception;

        public DeployFailed(UserId address, Exception exception)
        {
            Address = address;
            Exception = exception;
        }

        public override string ToString() =>
            $"{Address.Value} {Exception.GetType().Name}: {Exception.Message}";
    }

    /// <summary>
    ///     Facts fed into the self-profile FSM. They state what happened, never what to do; the update derives the intention.
    ///     Identity messages come from the identity cache. <c>ProfileEdited</c> means the user finished composing a new
    ///     profile version for the current identity. Fetch and deploy results are produced by the command executor and
    ///     carry the address the IO was started for, so a result that arrives after the identity changed is recognised as stale.
    /// </summary>
    [REnum(EnumUnderlyingType.Byte)]
    [REnumField(typeof(UserId), "IdentityChanged")]
    [REnumFieldEmpty("IdentityCleared")]
    [REnumField(typeof(FetchSucceeded))]
    [REnumField(typeof(UserId), "FetchNotFound")]
    [REnumField(typeof(FetchFailed))]
    [REnumField(typeof(Profile), "ProfileEdited")]
    [REnumField(typeof(DeploySucceeded))]
    [REnumField(typeof(DeployFailed))]
    public readonly partial struct SelfProfileMsg
    {
        /// <summary>
        ///     The address the message was produced for. Null for <c>IdentityCleared</c> and <c>ProfileEdited</c>,
        ///     which are about whatever identity is current.
        /// </summary>
        public UserId? Address => Match<UserId?>(
            onIdentityChanged: static address => address,
            onIdentityCleared: static () => null,
            onFetchSucceeded: static m => m.Address,
            onFetchNotFound: static address => address,
            onFetchFailed: static m => m.Address,
            onProfileEdited: static _ => null,
            onDeploySucceeded: static m => m.Address,
            onDeployFailed: static m => m.Address
        );
    }
}
