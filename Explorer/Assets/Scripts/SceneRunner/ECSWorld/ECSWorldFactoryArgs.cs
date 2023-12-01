using CrdtEcsBridge.UpdateGate;
using DCL.PluginSystem.World.Dependencies;
using SceneRunner.Scene;

namespace SceneRunner.ECSWorld
{
    public readonly struct ECSWorldFactoryArgs
    {
        public readonly ECSWorldInstanceSharedDependencies SharedDependencies;
        public readonly ISystemGroupsUpdateGate SystemGroupsUpdateGate;
        public readonly ISceneData SceneData;


        public ECSWorldFactoryArgs(
            ECSWorldInstanceSharedDependencies sharedDependencies,
            ISystemGroupsUpdateGate systemGroupsUpdateGate,
            ISceneData sceneData)
        {
            SharedDependencies = sharedDependencies;
            SystemGroupsUpdateGate = systemGroupsUpdateGate;
            SceneData = sceneData;
        }
    }
}
