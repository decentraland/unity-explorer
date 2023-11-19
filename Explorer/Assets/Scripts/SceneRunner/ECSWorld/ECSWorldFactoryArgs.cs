using CrdtEcsBridge.UpdateGate;
using DCL.PluginSystem.World.Dependencies;

namespace SceneRunner.ECSWorld
{
    public readonly struct ECSWorldFactoryArgs
    {
        public readonly ECSWorldInstanceSharedDependencies SharedDependencies;
        public readonly ISystemGroupsUpdateGate SystemGroupsUpdateGate;

        public ECSWorldFactoryArgs(ECSWorldInstanceSharedDependencies sharedDependencies, ISystemGroupsUpdateGate systemGroupsUpdateGate)
        {
            SharedDependencies = sharedDependencies;
            SystemGroupsUpdateGate = systemGroupsUpdateGate;
        }
    }
}
