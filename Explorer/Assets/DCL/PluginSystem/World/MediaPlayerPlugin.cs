using Arch.SystemGroups;
using DCL.Optimization.PerformanceBudgeting;
using DCL.Optimization.Pools;
using DCL.PluginSystem.World.Dependencies;
using DCL.ResourcesUnloading;
using DCL.SDKComponents.MediaStream.Wrapper;
using DCL.WebRequests;
using ECS.LifeCycle;
using System.Collections.Generic;
using UnityEngine;

namespace DCL.PluginSystem.World
{
    public class MediaPlayerPlugin : IDCLWorldPluginWithoutSettings
    {
        private readonly IWebRequestController webRequestController;
        private readonly MediaPlayerPluginWrapper mediaPlayerPluginWrapper;

        public MediaPlayerPlugin(ECSWorldSingletonSharedDependencies sharedDependencies, IWebRequestController webRequestController, CacheCleaner cacheCleaner, IExtendedObjectPool<Texture2D> videoTexturePool, IPerformanceBudget frameTimeBudget)
        {
            mediaPlayerPluginWrapper = new MediaPlayerPluginWrapper(sharedDependencies.ComponentPoolsRegistry, webRequestController, cacheCleaner, videoTexturePool, frameTimeBudget);
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in ECSWorldInstanceSharedDependencies sharedDependencies, in PersistentEntities _, List<IFinalizeWorldSystem> finalizeWorldSystems, List<ISceneIsCurrentListener> sceneIsCurrentListeners)
        {
            mediaPlayerPluginWrapper.InjectToWorld(ref builder, sharedDependencies.SceneData, sharedDependencies.SceneStateProvider, sharedDependencies.EcsToCRDTWriter, finalizeWorldSystems);
        }
    }
}
