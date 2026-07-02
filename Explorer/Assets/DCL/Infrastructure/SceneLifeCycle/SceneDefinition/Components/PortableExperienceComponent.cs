using CommunicationData.URLHelpers;

namespace ECS.SceneLifeCycle.SceneDefinition
{
    /// <summary>
    ///     A component used to indicate which scenes are PX and filter them from certain queries.
    ///     Also contains an ID to help filter which entities belong to certain PX.
    ///     Carries the PX world's <see cref="RealmData" /> (name, comms secret) so the scene loading flow
    ///     can establish the authoritative scene-comms room for the experience.
    /// </summary>
    public struct PortableExperienceComponent
    {
        public readonly ENS Ens;
        public readonly IRealmData RealmData;

        public PortableExperienceComponent(ENS ens, IRealmData realmData)
        {
            Ens = ens;
            RealmData = realmData;
        }
    };
}
