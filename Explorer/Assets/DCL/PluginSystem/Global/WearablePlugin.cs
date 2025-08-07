using Arch.Core;
using Arch.SystemGroups;
using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.AvatarRendering.Thumbnails.Systems;
using DCL.AvatarRendering.Wearables.Components;
using DCL.AvatarRendering.Wearables.Components.Intentions;
using DCL.AvatarRendering.Wearables.Helpers;
using DCL.AvatarRendering.Wearables.Systems;
using DCL.AvatarRendering.Wearables.Systems.Load;
using DCL.PluginSystem;
using DCL.PluginSystem.Global;
using DCL.ResourcesUnloading;
using DCL.WebRequests;
using ECS;
using ECS.StreamableLoading.Cache;
using SceneRunner.Scene;
using System;
using System.Threading;

namespace DCL.AvatarRendering.Wearables
{
    public class WearablePlugin : IDCLGlobalPlugin<WearablePlugin.WearableSettings>
    {
        //Should be taken from the catalyst
        private static readonly URLSubdirectory EXPLORER_SUBDIRECTORY = URLSubdirectory.FromString("/explorer/");
        private static readonly URLSubdirectory WEARABLES_COMPLEMENT_URL = URLSubdirectory.FromString("/wearables/");
        private static readonly URLSubdirectory WEARABLES_EMBEDDED_SUBDIRECTORY = URLSubdirectory.FromString("/Wearables/");
        private readonly string builderContentURL;
        private readonly IAssetsProvisioner assetsProvisioner;
        private readonly IWebRequestController webRequestController;
        private readonly bool builderCollectionsPreview;
        private readonly IRealmData realmData;
        private readonly IWearableStorage wearableStorage;

        public WearablePlugin(IAssetsProvisioner assetsProvisioner,
            IWebRequestController webRequestController,
            IRealmData realmData,
            CacheCleaner cacheCleaner,
            IWearableStorage wearableStorage,
            string builderContentURL,
            bool builderCollectionsPreview)
        {
            this.wearableStorage = wearableStorage;
            this.assetsProvisioner = assetsProvisioner;
            this.webRequestController = webRequestController;
            this.realmData = realmData;
            this.builderContentURL = builderContentURL;
            this.builderCollectionsPreview = builderCollectionsPreview;

            cacheCleaner.Register(this.wearableStorage);
        }

        public void Dispose() { }

        public async UniTask InitializeAsync(WearableSettings settings, CancellationToken ct)
        {
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<World> builder, in GlobalPluginArguments arguments)
        {
            LoadWearablesByParamSystem.InjectToWorld(ref builder, webRequestController, new NoCache<WearablesResponse, GetWearableByParamIntention>(false, false), realmData, EXPLORER_SUBDIRECTORY, WEARABLES_COMPLEMENT_URL, wearableStorage, builderContentURL);
            LoadWearablesDTOByPointersSystem.InjectToWorld(ref builder, webRequestController, new NoCache<WearablesDTOList, GetWearableDTOByPointersIntention>(false, false));
            LoadDefaultWearablesSystem.InjectToWorld(ref builder, wearableStorage);

            FinalizeAssetBundleWearableLoadingSystem.InjectToWorld(ref builder, wearableStorage, realmData);
            if (builderCollectionsPreview)
                FinalizeRawWearableLoadingSystem.InjectToWorld(ref builder, wearableStorage, realmData);

            ResolveAvatarAttachmentThumbnailSystem.InjectToWorld(ref builder);
            ResolveWearablePromisesSystem.InjectToWorld(ref builder, wearableStorage, realmData, WEARABLES_EMBEDDED_SUBDIRECTORY);
        }
    }
}
