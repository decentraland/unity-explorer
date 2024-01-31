using Arch.SystemGroups;
using DCL.Optimization.Pools;
using DCL.PluginSystem.World.Dependencies;
using DCL.ResourcesUnloading;
using DCL.SDKComponents.MediaStream.Wrapper;
using ECS.LifeCycle;
using System.Collections.Generic;
using UnityEngine;

namespace DCL.PluginSystem.World
{
    public class MediaPlayerPlugin : IDCLWorldPluginWithoutSettings
    {
        private readonly MediaPlayerPluginWrapper mediaPlayerPluginWrapper;

        public MediaPlayerPlugin(ECSWorldSingletonSharedDependencies sharedDependencies, CacheCleaner cacheCleaner, IExtendedObjectPool<Texture2D> videoTexturePool)
        {
            mediaPlayerPluginWrapper = new MediaPlayerPluginWrapper(sharedDependencies.ComponentPoolsRegistry, cacheCleaner, videoTexturePool);
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in ECSWorldInstanceSharedDependencies sharedDependencies, in PersistentEntities _, List<IFinalizeWorldSystem> finalizeWorldSystems)
        {
            mediaPlayerPluginWrapper.InjectToWorld(ref builder, sharedDependencies.SceneStateProvider, sharedDependencies.EcsToCRDTWriter, finalizeWorldSystems);
        }

        public void InjectToEmptySceneWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in EmptyScenesWorldSharedDependencies dependencies) { }
    }
}
