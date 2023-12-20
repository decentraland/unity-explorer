using Arch.SystemGroups;
using DCL.PluginSystem.World.Dependencies;
using ECS.LifeCycle;
using ECS.Unity.AudioStreams.Systems;
using System.Collections.Generic;

namespace DCL.PluginSystem.World
{
    public class AudioStreamPlugin : IDCLWorldPluginWithoutSettings
    {
        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in ECSWorldInstanceSharedDependencies sharedDependencies, in PersistentEntities persistentEntities, List<IFinalizeWorldSystem> finalizeWorldSystems)
        {
            InstantiateAudioStreamSystem.InjectToWorld(ref builder);
        }

        public void InjectToEmptySceneWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in EmptyScenesWorldSharedDependencies dependencies) { }
    }
}
