using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.Ipfs;
using DCL.PluginSystem.Global;
using DCL.PluginSystem.World.Dependencies;
using DCL.Profiles;
using DCL.ResourcesUnloading;
using DCL.Utility;
using DCL.WebRequests;
using ECS.LifeCycle;
using ECS.StreamableLoading.Cache;
using ECS.StreamableLoading.Cache.Disk;
using ECS.StreamableLoading.Textures;
using System;
using System.Collections.Generic;
using System.Threading;

namespace DCL.PluginSystem.World
{
    public class TexturesLoadingPlugin : IDCLWorldPluginWithoutSettings, IDCLGlobalPluginWithoutSettings
    {
        private readonly IWebRequestController webRequestController;
        private readonly IDiskCache<TextureData> diskCache;
        private readonly IProfileRepository profileRepository;
        private readonly ILaunchMode launchMode;

        // Always created and registered, whatever the launch mode: the global world consumes it
        // unconditionally. Scene worlds opt out per scene definition in InjectToWorld.
        private readonly TexturesCache<GetTextureIntention> texturesCache;

        public TexturesLoadingPlugin(IWebRequestController webRequestController, CacheCleaner cacheCleaner, IDiskCache<TextureData> diskCache, ILaunchMode launchMode,
            IProfileRepository profileRepository)
        {
            this.webRequestController = webRequestController;
            this.diskCache = diskCache;
            this.launchMode = launchMode;
            this.profileRepository = profileRepository;

            texturesCache = new TexturesCache<GetTextureIntention>();
            cacheCleaner.Register(texturesCache);
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in ECSWorldInstanceSharedDependencies sharedDependencies, in SystemsDependencies systemsDependencies, in PersistentEntities persistentEntities, List<IFinalizeWorldSystem> finalizeWorldSystems, List<ISceneIsCurrentListener> sceneIsCurrentListeners)
        {
            IStreamableCache<TextureData, GetTextureIntention> cache;

            if (launchMode.CurrentMode == LaunchMode.LocalSceneDevelopment && LocalSceneDevHashes.IsPathOnly(sharedDependencies.SceneData.SceneEntityDefinition))
                cache = new NoCache<TextureData, GetTextureIntention>(true, true);
            else
                cache = texturesCache;

            LoadTextureSystem.InjectToWorld(ref builder, cache, webRequestController, diskCache, profileRepository);
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in GlobalPluginArguments arguments) =>
            LoadGlobalTextureSystem.InjectToWorld(ref builder, texturesCache, webRequestController, diskCache, profileRepository);

        UniTask IDCLPlugin<NoExposedPluginSettings>.InitializeAsync(NoExposedPluginSettings settings, CancellationToken ct) =>
            UniTask.CompletedTask;

        void IDisposable.Dispose() { }
    }
}
