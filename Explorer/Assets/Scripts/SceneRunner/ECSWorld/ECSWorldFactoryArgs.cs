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
        public readonly ISceneData SceneData;
        public ECSWorldFactoryArgs(ECSWorldInstanceSharedDependencies sharedDependencies, ISystemGroupsUpdateGate systemGroupsUpdateGate, IPartitionComponent scenePartition, ISceneData sceneData)
        {
            SharedDependencies = sharedDependencies;
            SystemGroupsUpdateGate = systemGroupsUpdateGate;
            ScenePartition = scenePartition;
            SceneData = sceneData;
        }
    }
}
