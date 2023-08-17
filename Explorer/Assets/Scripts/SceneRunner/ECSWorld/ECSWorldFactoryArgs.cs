using CrdtEcsBridge.UpdateGate;
using DCL.PluginSystem.World.Dependencies;
using ECS.Prioritization.Components;
using SceneRunner.Scene;

namespace SceneRunner.ECSWorld
{
    public readonly struct ECSWorldFactoryArgs
    {
        public readonly ECSWorldInstanceSharedDependencies SharedDependencies;
        public readonly ISystemGroupsUpdateGate SystemGroupsUpdateGate;
        public readonly IPartitionComponent ScenePartition;
        public readonly ISceneStateProvider SceneStateProvider;

        public ECSWorldFactoryArgs(ECSWorldInstanceSharedDependencies sharedDependencies, ISystemGroupsUpdateGate systemGroupsUpdateGate, IPartitionComponent scenePartition, ISceneStateProvider sceneStateProvider)
        {
            SharedDependencies = sharedDependencies;
            SystemGroupsUpdateGate = systemGroupsUpdateGate;
            ScenePartition = scenePartition;
            SceneStateProvider = sceneStateProvider;
        }
    }
}
