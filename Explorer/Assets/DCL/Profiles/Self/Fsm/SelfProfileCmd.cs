using REnum;

namespace DCL.Profiles.Self
{
    public readonly struct DeployCmd
    {
        public readonly UserId Address;
        public readonly Profile Profile;

        public DeployCmd(UserId address, Profile profile)
        {
            Address = address;
            Profile = profile;
        }

        public override string ToString() =>
            $"{Address.Value} v{Profile.Version}";
    }

    /// <summary>
    ///     Intents the self-profile update emits. The executor performs them; the model never observes them.
    ///     <c>None</c> = the message applied and there is nothing to do; <c>Ignore</c> = the message did not apply, the payload
    ///     says why and the executor logs it; <c>ResetLocalState</c> = drop equipped wearables, emotes and owned-NFT registries;
    ///     <c>Fetch</c> = read the profile of the address, cancelling any read in flight; <c>Publish</c> = make the profile
    ///     the local truth (cache, player entity); <c>Deploy</c> = send the profile to the catalyst and re-read it;
    ///     <c>Batch</c> = perform the commands in order.
    /// </summary>
    [REnum(EnumUnderlyingType.Byte)]
    [REnumFieldEmpty("None")]
    [REnumField(typeof(string), "Ignore")]
    [REnumFieldEmpty("ResetLocalState")]
    [REnumField(typeof(UserId), "Fetch")]
    [REnumField(typeof(Profile), "Publish")]
    [REnumField(typeof(DeployCmd), "Deploy")]
    [REnumField(typeof(SelfProfileCmd[]), "Batch")]
    public readonly partial struct SelfProfileCmd { }
}
