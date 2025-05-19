using Arch.Core;
using Arch.SystemGroups;
using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.AvatarRendering.Loading.Components;
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
using ECS.StreamableLoading.GLTF;
using ECS.StreamableLoading.GLTF.DownloadProvider;
using Newtonsoft.Json;
using SceneRunner.Scene;
using System;
using System.Threading;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace DCL.AvatarRendering.Wearables
{
    public class WearablePlugin : IDCLGlobalPlugin<WearablePlugin.WearableSettings>
    {
        //Should be taken from the catalyst
        private static readonly URLSubdirectory EXPLORER_SUBDIRECTORY = URLSubdirectory.FromString("/explorer/");
        private static readonly URLSubdirectory WEARABLES_COMPLEMENT_URL = URLSubdirectory.FromString("/wearables/");
        private static readonly URLSubdirectory WEARABLES_EMBEDDED_SUBDIRECTORY = URLSubdirectory.FromString("/Wearables/");
        private readonly URLDomain assetBundleURL;
        private readonly string builderContentURL;
        private readonly IAssetsProvisioner assetsProvisioner;
        private readonly IWebRequestController webRequestController;
        private readonly bool builderCollectionsPreview;
        private readonly IRealmData realmData;
        private readonly IWearableStorage wearableStorage;

        private WearablesDTOList defaultWearablesDTOs;
        private GameObject defaultEmptyWearableAsset;

        public WearablePlugin(IAssetsProvisioner assetsProvisioner,
            IWebRequestController webRequestController,
            IRealmData realmData,
            URLDomain assetBundleURL,
            CacheCleaner cacheCleaner,
            IWearableStorage wearableStorage,
            string builderContentURL,
            bool builderCollectionsPreview)
        {
            this.wearableStorage = wearableStorage;
            this.assetsProvisioner = assetsProvisioner;
            this.webRequestController = webRequestController;
            this.realmData = realmData;
            this.assetBundleURL = assetBundleURL;
            this.builderContentURL = builderContentURL;
            this.builderCollectionsPreview = builderCollectionsPreview;

            cacheCleaner.Register(this.wearableStorage);
        }

        public void Dispose() { }

        public async UniTask InitializeAsync(WearableSettings settings, CancellationToken ct)
        {
            ProvidedAsset<TextAsset> defaultWearableDefinition = await assetsProvisioner.ProvideMainAssetAsync(settings.defaultWearablesDefinition, ct: ct);

            var repoolableList = RepoolableList<WearableDTO>.NewList();
            var partialTargetList = repoolableList.List;
            partialTargetList.Capacity = 64;
            JsonConvert.PopulateObject(defaultWearableDefinition.Value.text, partialTargetList);

            defaultWearablesDTOs = new WearablesDTOList(repoolableList);

            var defaultEmptyWearable =
                await assetsProvisioner.ProvideMainAssetAsync(settings.defaultEmptyWearable, ct: ct);
            defaultEmptyWearableAsset = defaultEmptyWearable.Value;
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<World> builder, in GlobalPluginArguments arguments)
        {
            LoadWearablesByParamSystem.InjectToWorld(ref builder, webRequestController, new NoCache<WearablesResponse, GetWearableByParamIntention>(false, false), realmData, EXPLORER_SUBDIRECTORY, WEARABLES_COMPLEMENT_URL, wearableStorage, builderContentURL);
            LoadWearablesDTOByPointersSystem.InjectToWorld(ref builder, webRequestController, new NoCache<WearablesDTOList, GetWearableDTOByPointersIntention>(false, false));
            LoadWearableAssetBundleManifestSystem.InjectToWorld(ref builder, new NoCache<SceneAssetBundleManifest, GetWearableAssetBundleManifestIntention>(true, true), assetBundleURL, webRequestController);
            LoadDefaultWearablesSystem.InjectToWorld(ref builder, defaultWearablesDTOs, defaultEmptyWearableAsset, wearableStorage);

            FinalizeAssetBundleWearableLoadingSystem.InjectToWorld(ref builder, wearableStorage, realmData);
            if (builderCollectionsPreview)
            {
                FinalizeRawWearableLoadingSystem.InjectToWorld(ref builder, wearableStorage, realmData);

                // TODO: Isolate only 1 LoadGLTFSystem instance in a dedicated global plugin...

                /*LoadGLTFSystem.InjectToWorld(ref builder,
                    NoCache<GLTFData, GetGLTFIntention>.INSTANCE,
                    webRequestController,
                    true,
                    true,
                    new GltFastGlobalDownloadStrategy(builderContentURL));*/
            }

            ResolveAvatarAttachmentThumbnailSystem.InjectToWorld(ref builder);
            ResolveWearablePromisesSystem.InjectToWorld(ref builder, wearableStorage, realmData, WEARABLES_EMBEDDED_SUBDIRECTORY);
        }

        [Serializable]
        public class WearableSettings : IDCLPluginSettings
        {
            [field: SerializeField]
            public AssetReferenceTextAsset defaultWearablesDefinition;

            [field: SerializeField] public AssetReferenceGameObject defaultEmptyWearable;
        }
    }
}
