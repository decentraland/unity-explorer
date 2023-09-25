using CrdtEcsBridge.UpdateGate;
using DCL.PluginSystem.World.Dependencies;
using ECS.Prioritization.Components;

namespace SceneRunner.ECSWorld
{
    public readonly struct ECSWorldFactoryArgs
    {
        public readonly ECSWorldInstanceSharedDependencies SharedDependencies;
        public readonly ISystemGroupsUpdateGate SystemGroupsUpdateGate;
        public readonly IPartitionComponent ScenePartition;

        public ECSWorldFactoryArgs(ECSWorldInstanceSharedDependencies sharedDependencies, ISystemGroupsUpdateGate systemGroupsUpdateGate, IPartitionComponent scenePartition)
        {
            SharedDependencies = sharedDependencies;
            SystemGroupsUpdateGate = systemGroupsUpdateGate;
            ScenePartition = scenePartition;
        }
    }
}
