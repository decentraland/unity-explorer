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

    public readonly struct DeployRequested
    {
        public readonly UserId Address;
        public readonly Profile Profile;

        /// <summary>When true the pending profile is published locally before the catalyst confirms it.</summary>
        public readonly bool Optimistic;

        public DeployRequested(UserId address, Profile profile, bool optimistic)
        {
            Address = address;
            Profile = profile;
            Optimistic = optimistic;
        }

        public override string ToString() =>
            $"{Address.Value} v{Profile.Version} optimistic {Optimistic}";
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
    ///     Facts fed into the self-profile FSM. Identity messages come from the identity cache; every other message
    ///     is produced by the command executor and carries the address the IO was started for, so a result
    ///     that arrives after the identity changed can be recognised as stale.
    ///     <c>IdentityChanged</c>, <c>FetchRequested</c> and <c>FetchNotFound</c> carry only that address.
    /// </summary>
    [REnum(EnumUnderlyingType.Byte)]
    [REnumField(typeof(UserId), "IdentityChanged")]
    [REnumFieldEmpty("IdentityCleared")]
    [REnumField(typeof(UserId), "FetchRequested")]
    [REnumField(typeof(FetchSucceeded))]
    [REnumField(typeof(UserId), "FetchNotFound")]
    [REnumField(typeof(FetchFailed))]
    [REnumField(typeof(DeployRequested))]
    [REnumField(typeof(DeploySucceeded))]
    [REnumField(typeof(DeployFailed))]
    public readonly partial struct SelfProfileMsg
    {
        /// <summary>
        ///     The address this message is about; null only for <c>IdentityCleared</c>.
        /// </summary>
        public UserId? Address => Match<UserId?>(
            onIdentityChanged: static address => address,
            onIdentityCleared: static () => null,
            onFetchRequested: static address => address,
            onFetchSucceeded: static m => m.Address,
            onFetchNotFound: static address => address,
            onFetchFailed: static m => m.Address,
            onDeployRequested: static m => m.Address,
            onDeploySucceeded: static m => m.Address,
            onDeployFailed: static m => m.Address
        );
    }
}
