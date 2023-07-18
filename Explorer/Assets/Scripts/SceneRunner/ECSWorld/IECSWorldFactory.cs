using CrdtEcsBridge.UpdateGate;
using ECS.Prioritization.Components;

namespace SceneRunner.ECSWorld
{
    public interface IECSWorldFactory
    {
        /// <summary>
        /// Create a new instance of the ECS world, all its systems and attach them to the player loop
        /// </summary>
        ECSWorldFacade CreateWorld(in ECSWorldInstanceSharedDependencies sharedDependencies, in ISystemGroupsUpdateGate systemGroupsUpdateGate, in IPartitionComponent scenePartition);
    }
}
