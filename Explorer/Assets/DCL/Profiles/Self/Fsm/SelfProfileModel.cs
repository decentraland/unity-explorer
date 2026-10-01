using DCL.Utility.Types;
using REnum;

namespace DCL.Profiles.Self
{
    /// <summary>
    ///     Model of the self-profile FSM while an identity is present. Knowledge and activity exist only together
    ///     with the address they belong to.
    /// </summary>
    public readonly struct Identified
    {
        public readonly UserId Address;
        public readonly ProfileKnowledge Knowledge;
        public readonly ProfileActivity Activity;

        public Identified(UserId address, ProfileKnowledge knowledge, ProfileActivity activity)
        {
            Address = address;
            Knowledge = knowledge;
            Activity = activity;
        }

        /// <summary>A fresh identity: nothing known, nothing in flight.</summary>
        public static Identified New(UserId address) =>
            new (address, ProfileKnowledge.Unknown(), ProfileActivity.Idle());

        public Identified WithKnowledge(ProfileKnowledge knowledge) =>
            new (Address, knowledge, Activity);

        public Identified WithActivity(ProfileActivity activity) =>
            new (Address, Knowledge, activity);

        public Identified With(ProfileKnowledge knowledge, ProfileActivity activity) =>
            new (Address, knowledge, activity);

        public override string ToString() =>
            $"{Address.Value} knowledge {Knowledge} activity {Activity}";
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
