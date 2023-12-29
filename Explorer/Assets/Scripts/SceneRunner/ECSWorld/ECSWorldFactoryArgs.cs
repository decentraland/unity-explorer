using CrdtEcsBridge.UpdateGate;
using DCL.PluginSystem.World.Dependencies;
using JetBrains.Annotations;
using SceneRunner.Scene;

namespace SceneRunner.ECSWorld
{
    public readonly struct ECSWorldFactoryArgs
    {
        public readonly ECSWorldInstanceSharedDependencies SharedDependencies;
        public readonly ISystemGroupsUpdateGate SystemGroupsUpdateGate;
        public readonly ISceneData SceneData;
        [CanBeNull] public readonly SceneReadinessReport SceneReadinessReport;

        public ECSWorldFactoryArgs(
            ECSWorldInstanceSharedDependencies sharedDependencies,
            ISystemGroupsUpdateGate systemGroupsUpdateGate,
            ISceneData sceneData, [CanBeNull] SceneReadinessReport sceneReadinessReport)
        {
            SharedDependencies = sharedDependencies;
            SystemGroupsUpdateGate = systemGroupsUpdateGate;
            SceneData = sceneData;
            SceneReadinessReport = sceneReadinessReport;
        }
    }
}
