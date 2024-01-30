using Arch.SystemGroups;
using DCL.Optimization.Pools;
using DCL.PluginSystem.World.Dependencies;
using DCL.ResourcesUnloading;
using DCL.SDKComponents.VideoPlayer.Wrapper;
using ECS.LifeCycle;
using System.Collections.Generic;
using UnityEngine;

namespace DCL.PluginSystem.World
{
    public class VideoPlayerPlugin : IDCLWorldPluginWithoutSettings
    {
        private readonly VideoPlayerPluginWrapper videoPlayerPluginWrapper;

        public VideoPlayerPlugin(ECSWorldSingletonSharedDependencies sharedDependencies, CacheCleaner cacheCleaner, IExtendedObjectPool<Texture2D> videoTexturePool)
        {
            videoPlayerPluginWrapper = new VideoPlayerPluginWrapper(sharedDependencies.ComponentPoolsRegistry, cacheCleaner, videoTexturePool);
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in ECSWorldInstanceSharedDependencies sharedDependencies, in PersistentEntities _, List<IFinalizeWorldSystem> __)
        {
            videoPlayerPluginWrapper.InjectToWorld(ref builder, sharedDependencies.SceneStateProvider, sharedDependencies.EcsToCRDTWriter);
        }

        public void InjectToEmptySceneWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in EmptyScenesWorldSharedDependencies dependencies) { }
    }
}
