using Arch.SystemGroups;
using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.PluginSystem.Global;
using DCL.PluginSystem.World.Dependencies;
using DCL.Profiles;
using DCL.ResourcesUnloading;
using DCL.Utilities;
using DCL.WebRequests;
using ECS.LifeCycle;
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
        private readonly TexturesCache<GetTextureIntention> texturesCache;

        public TexturesLoadingPlugin(IWebRequestController webRequestController, CacheCleaner cacheCleaner, IDiskCache<TextureData> diskCache,
            IProfileRepository profileRepository)
        {
            this.webRequestController = webRequestController;
            this.diskCache = diskCache;
            this.profileRepository = profileRepository;

            texturesCache = new TexturesCache<GetTextureIntention>();
            cacheCleaner.Register(texturesCache);
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in ECSWorldInstanceSharedDependencies sharedDependencies, in SystemsDependencies systemsDependencies, in PersistentEntities persistentEntities, List<IFinalizeWorldSystem> finalizeWorldSystems, List<ISceneIsCurrentListener> sceneIsCurrentListeners) =>
            LoadTextureSystem.InjectToWorld(ref builder, texturesCache, webRequestController, diskCache, profileRepository);

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in GlobalPluginArguments arguments) =>
            LoadGlobalTextureSystem.InjectToWorld(ref builder, texturesCache, webRequestController, diskCache, profileRepository);

        UniTask IDCLPlugin<NoExposedPluginSettings>.InitializeAsync(NoExposedPluginSettings settings, CancellationToken ct) =>
            UniTask.CompletedTask;

        void IDisposable.Dispose() { }
    }
}
