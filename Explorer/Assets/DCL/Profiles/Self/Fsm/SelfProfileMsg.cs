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

        /// <summary>The profile instance the deploy was started with. Identifies which deploy this result belongs to.</summary>
        public readonly Profile Sent;

        /// <summary>The profile as re-read from the catalyst after the deploy, not the one that was sent.</summary>
        public readonly Profile Saved;

        public DeploySucceeded(UserId address, Profile sent, Profile saved)
        {
            Address = address;
            Sent = sent;
            Saved = saved;
        }

        public override string ToString() =>
            $"{Address.Value} sent v{Sent.Version} saved v{Saved.Version}";
    }

    public readonly struct DeployFailed
    {
        public readonly UserId Address;

        /// <summary>The profile instance the deploy was started with. Identifies which deploy this result belongs to.</summary>
        public readonly Profile Sent;

        public readonly Exception Exception;

        public DeployFailed(UserId address, Profile sent, Exception exception)
        {
            Address = address;
            Sent = sent;
            Exception = exception;
        }

        public override string ToString() =>
            $"{Address.Value} sent v{Sent.Version} {Exception.GetType().Name}: {Exception.Message}";
    }

    /// <summary>
    ///     Facts fed into the self-profile FSM. They state what happened, never what to do; the update derives the intention.
    ///     Identity messages come from the identity cache. <c>DeployProfileEditRequested</c> means the user finished composing a new
    ///     profile version for the current identity. Fetch and deploy results are produced by the command executor and
    ///     carry the address the IO was started for, so a result that arrives after the identity changed is recognised as stale.
    ///     Deploy results also carry the profile instance that was sent, so the result of a superseded deploy is recognised too.
    ///     <c>ProfileRefetchRequested</c> asks for a new fetch of the current identity's profile after a failed one.
    /// </summary>
    [REnum(EnumUnderlyingType.Byte)]
    [REnumField(typeof(UserId), "IdentityChanged")]
    [REnumFieldEmpty("IdentityCleared")]
    [REnumField(typeof(FetchSucceeded))]
    [REnumField(typeof(UserId), "FetchNotFound")]
    [REnumField(typeof(FetchFailed))]
    [REnumField(typeof(Profile), "DeployProfileOnEditRequested")]
    [REnumField(typeof(DeploySucceeded))]
    [REnumField(typeof(DeployFailed))]
    [REnumFieldEmpty("ProfileRefetchRequested")]
    public readonly partial struct SelfProfileMsg
    {
        /// <summary>
        ///     The address the message was produced for. Null for <c>IdentityCleared</c>, <c>DeployProfileEditRequested</c> and
        ///     <c>ProfileRefetchRequested</c>, which are about whatever identity is current.
        /// </summary>
        public UserId? Address => Match<UserId?>(
            onIdentityChanged: static address => address,
            onIdentityCleared: static () => null,
            onFetchSucceeded: static m => m.Address,
            onFetchNotFound: static address => address,
            onFetchFailed: static m => m.Address,
            onDeployProfileEditRequested: static _ => null,
            onDeploySucceeded: static m => m.Address,
            onDeployFailed: static m => m.Address,
            onProfileRefetchRequested: static () => null
        );
    }
}
