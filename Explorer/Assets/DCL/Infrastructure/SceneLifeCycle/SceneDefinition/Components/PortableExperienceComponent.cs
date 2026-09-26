using CommunicationData.URLHelpers;

namespace ECS.SceneLifeCycle.SceneDefinition
{
    /// <summary>
    ///     A component used to indicate which scenes are PX and filter them from certain queries.
    ///     Also contains an ID to help filter which entities belong to certain PX
    /// </summary>
    public struct PortableExperienceComponent
    {
        public readonly ENS Ens;

        public PortableExperienceComponent(ENS ens)
        {
            Ens = ens;
        }
    };
}
