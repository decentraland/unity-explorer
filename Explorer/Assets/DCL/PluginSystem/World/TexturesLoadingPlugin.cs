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
using ECS.StreamableLoading.Cache;
using Global.Dynamic.LaunchModes;

namespace DCL.PluginSystem.World
{
    public class TexturesLoadingPlugin : IDCLWorldPluginWithoutSettings, IDCLGlobalPluginWithoutSettings
    {
        private readonly IWebRequestController webRequestController;
        private readonly IDiskCache<Texture2DData> diskCache;
        private readonly IStreamableCache<Texture2DData, GetTextureIntention> texturesCache;
        private readonly ProfilePictureUrlProvider avatarTextureProvider;

        public TexturesLoadingPlugin(IWebRequestController webRequestController, CacheCleaner cacheCleaner, IDiskCache<Texture2DData> diskCache, ILaunchMode launchMode,
            ObjectProxy<IProfileRepository> profileRepository)
        {
            this.webRequestController = webRequestController;
            this.diskCache = diskCache;
            avatarTextureProvider = new ProfilePictureUrlProvider(profileRepository);

            if (launchMode.CurrentMode == LaunchMode.LocalSceneDevelopment)
                texturesCache = new NoCache<Texture2DData, GetTextureIntention>(true, true);
            else
            {
                texturesCache = new TexturesCache<GetTextureIntention>();
                cacheCleaner.Register((TexturesCache<GetTextureIntention>)texturesCache);
            }
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in ECSWorldInstanceSharedDependencies sharedDependencies, in PersistentEntities persistentEntities, List<IFinalizeWorldSystem> finalizeWorldSystems, List<ISceneIsCurrentListener> sceneIsCurrentListeners)
        {
            LoadTextureSystem.InjectToWorld(ref builder, texturesCache, webRequestController, diskCache, avatarTextureProvider);
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in GlobalPluginArguments arguments)
        {
            LoadGlobalTextureSystem.InjectToWorld(ref builder, texturesCache, webRequestController, diskCache, avatarTextureProvider);
        }

        UniTask IDCLPlugin<NoExposedPluginSettings>.InitializeAsync(NoExposedPluginSettings settings, CancellationToken ct) =>
            UniTask.CompletedTask;

        void IDisposable.Dispose() { }

        private class ProfilePictureUrlProvider : IAvatarTextureUrlProvider
        {
            private readonly ObjectProxy<IProfileRepository> profileRepository;

            public ProfilePictureUrlProvider(ObjectProxy<IProfileRepository> profileRepository)
            {
                this.profileRepository = profileRepository;
            }

            public async UniTask<URLAddress?> GetAsync(string userId, CancellationToken ct)
            {
                if (!profileRepository.Configured) return null;
                Profile? profile = await profileRepository.Object!.GetAsync(userId, ct);
                return profile?.Avatar.FaceSnapshotUrl;
            }
        }
    }
}
