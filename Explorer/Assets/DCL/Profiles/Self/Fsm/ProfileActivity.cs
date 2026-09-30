using REnum;

namespace DCL.Profiles.Self
{
    public readonly struct Deploying
    {
        public readonly Profile Pending;

        /// <summary>Knowledge at the moment the deploy started; the model reverts to it when the deploy fails.</summary>
        public readonly ProfileKnowledge Before;

        public Deploying(Profile pending, ProfileKnowledge before)
        {
            Pending = pending;
            Before = before;
        }

        public override string ToString() =>
            $"pending v{Pending.Version} before {Before}";
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
