using DCL.Utility.Types;
using REnum;
using System;

namespace DCL.Profiles.Self
{
    /// <summary>A deploy that did not land: the edit that was sent and why it failed.</summary>
    public readonly struct DeployFailure
    {
        public readonly Profile Sent;
        public readonly Exception Exception;

        public DeployFailure(Profile sent, Exception exception)
        {
            Sent = sent;
            Exception = exception;
        }

        public override string ToString() =>
            $"v{Sent.Version} {Exception.GetType().Name}: {Exception.Message}";
    }

    /// <summary>
    ///     Model of the self-profile FSM while an identity is present. Knowledge and activity exist only together
    ///     with the address they belong to.
    /// </summary>
    public readonly struct Identified
    {
        public readonly UserId Address;
        public readonly ProfileKnowledge Knowledge;
        public readonly ProfileActivity Activity;

        /// <summary>The last failed deploy of this identity, if any.</summary>
        public readonly Option<DeployFailure> LastDeployFailure;

        public Identified(UserId address, ProfileKnowledge knowledge, ProfileActivity activity)
            : this(address, knowledge, activity, Option<DeployFailure>.None) { }

        public Identified(UserId address, ProfileKnowledge knowledge, ProfileActivity activity, Option<DeployFailure> lastDeployFailure)
        {
            Address = address;
            Knowledge = knowledge;
            Activity = activity;
            LastDeployFailure = lastDeployFailure;
        }

        /// <summary>A fresh identity: nothing known, nothing in flight.</summary>
        public static Identified New(UserId address) =>
            new (address, ProfileKnowledge.Unknown(), ProfileActivity.Idle());

        public Identified WithKnowledge(ProfileKnowledge knowledge) =>
            new (Address, knowledge, Activity, LastDeployFailure);

        public Identified WithActivity(ProfileActivity activity) =>
            new (Address, Knowledge, activity, LastDeployFailure);

        public Identified With(ProfileKnowledge knowledge, ProfileActivity activity) =>
            new (Address, knowledge, activity, LastDeployFailure);

        public Identified With(ProfileKnowledge knowledge, ProfileActivity activity, Option<DeployFailure> lastDeployFailure) =>
            new (Address, knowledge, activity, lastDeployFailure);

        public override string ToString() =>
            LastDeployFailure.Has
                ? $"{Address.Value} knowledge {Knowledge} activity {Activity} last deploy failure {LastDeployFailure.Value}"
                : $"{Address.Value} knowledge {Knowledge} activity {Activity}";
    }

    /// <summary>
    ///     Immutable model of the self-profile FSM. <c>NoIdentity</c> = no wallet is signed in, so there is nothing to know
    ///     and nothing to do; <c>Identified</c> = the address plus what is known about its profile and what is in flight.
    /// </summary>
    [REnum(EnumUnderlyingType.Byte)]
    [REnumFieldEmpty("NoIdentity")]
    [REnumField(typeof(Identified))]
    public readonly partial struct SelfProfileModel
    {
        /// <summary>The trusted profile of the current identity, when there is one.</summary>
        public Option<Profile> KnownProfile =>
            IsIdentified(out Identified identified) && identified.Knowledge.IsKnown(out Profile known)
                ? Option<Profile>.Some(known)
                : Option<Profile>.None;
    }
}
