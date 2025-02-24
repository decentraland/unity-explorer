using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.PluginSystem.Global;
using DCL.PluginSystem.World.Dependencies;
using DCL.ResourcesUnloading;
using DCL.WebRequests;
using ECS.LifeCycle;
using ECS.StreamableLoading.Cache.Disk;
using ECS.StreamableLoading.Textures;
using System;
using System.Collections.Generic;
using System.Threading;
using ECS.StreamableLoading.Cache;
using Global.Dynamic.LaunchModes;

namespace DCL.PluginSystem.World
{
    public class TexturesLoadingPlugin : IDCLWorldPluginWithoutSettings, IDCLGlobalPluginWithoutSettings
    {
        private readonly IWebRequestController webRequestController;
        private readonly IDiskCache<Texture2DData> diskCache;

        private readonly IStreamableCache<Texture2DData, GetTextureIntention> texturesCache;

        public TexturesLoadingPlugin(IWebRequestController webRequestController, CacheCleaner cacheCleaner, IDiskCache<Texture2DData> diskCache, ILaunchMode launchMode)
        {
            this.webRequestController = webRequestController;
            this.diskCache = diskCache;
            if (launchMode.CurrentMode == LaunchMode.LocalSceneDevelopment)
            {
                texturesCache = new NoCache<Texture2DData, GetTextureIntention>(true, true);
            }
            else
            {
                texturesCache = new TexturesCache<GetTextureIntention>();
                cacheCleaner.Register((TexturesCache<GetTextureIntention>)texturesCache);
            }
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in ECSWorldInstanceSharedDependencies sharedDependencies, in PersistentEntities persistentEntities, List<IFinalizeWorldSystem> finalizeWorldSystems, List<ISceneIsCurrentListener> sceneIsCurrentListeners)
        {
            LoadSceneTextureSystem.InjectToWorld(ref builder, texturesCache, webRequestController, diskCache);
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in GlobalPluginArguments arguments)
        {
            LoadGlobalTextureSystem.InjectToWorld(ref builder, texturesCache, webRequestController, diskCache);
        }

        UniTask IDCLPlugin<NoExposedPluginSettings>.InitializeAsync(NoExposedPluginSettings settings, CancellationToken ct) =>
            UniTask.CompletedTask;

        void IDisposable.Dispose() { }
    }
}
