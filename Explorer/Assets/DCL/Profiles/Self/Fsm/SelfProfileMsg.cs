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

    public readonly struct DeployRequest
    {
        public readonly RequestId Id;
        public readonly Profile Edited;

        public DeployRequest(RequestId id, Profile edited)
        {
            Id = id;
            Edited = edited;
        }

        public override string ToString() =>
            $"{Id} v{Edited.Version}";
    }

    [REnum(EnumUnderlyingType.Byte)]
    [REnumField(typeof(UserId), "IdentityChanged")]
    [REnumFieldEmpty("IdentityCleared")]
    [REnumField(typeof(FetchSucceeded))]
    [REnumField(typeof(UserId), "FetchNotFound")]
    [REnumField(typeof(FetchFailed))]
    [REnumField(typeof(DeployRequest), "DeployProfileOnEditRequested")]
    [REnumField(typeof(DeploySucceeded))]
    [REnumField(typeof(DeployFailed))]
    [REnumField(typeof(RequestId), "ProfileReadRequested")]
    [REnumField(typeof(RequestId), "RequestClosed")]
    public readonly partial struct SelfProfileMsg
    {
        /// <summary>The address the message was produced for; null for the messages about whatever identity is current.</summary>
        public UserId? Address => Match<UserId?>(
            onIdentityChanged: static address => address,
            onIdentityCleared: static () => null,
            onFetchSucceeded: static m => m.Address,
            onFetchNotFound: static address => address,
            onFetchFailed: static m => m.Address,
            onDeployProfileOnEditRequested: static _ => null,
            onDeploySucceeded: static m => m.Address,
            onDeployFailed: static m => m.Address,
            onProfileReadRequested: static _ => null,
            onRequestClosed: static _ => null
        );
    }
}
