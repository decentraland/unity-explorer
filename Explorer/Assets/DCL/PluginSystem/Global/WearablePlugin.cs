using Arch.Core;
using Arch.SystemGroups;
using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.AvatarRendering.Wearables.Components;
using DCL.AvatarRendering.Wearables.Components.Intentions;
using DCL.AvatarRendering.Wearables.Helpers;
using DCL.AvatarRendering.Wearables.Systems;
using DCL.PluginSystem;
using DCL.PluginSystem.Global;
using DCL.ResourcesUnloading;
using DCL.WebRequests;
using ECS;
using ECS.StreamableLoading.Cache;
using Newtonsoft.Json;
using SceneRunner.Scene;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.AddressableAssets;
using Utility.Multithreading;

namespace DCL.AvatarRendering.Wearables
{
    public class WearablePlugin : IDCLGlobalPlugin<WearablePlugin.WearableSettings>
    {
        //Should be taken from the catalyst
        private static readonly URLSubdirectory EXPLORER_SUBDIRECTORY = URLSubdirectory.FromString("/explorer/");
        private static readonly URLSubdirectory WEARABLES_COMPLEMENT_URL = URLSubdirectory.FromString("/wearables/");
        private static readonly URLSubdirectory WEARABLES_EMBEDDED_SUBDIRECTORY = URLSubdirectory.FromString("/Wearables/");
        private readonly URLDomain assetBundleURL;
        private readonly IAssetsProvisioner assetsProvisioner;
        private readonly IWebRequestController webRequestController;

        private readonly IRealmData realmData;
        private readonly IWearableCatalog wearableCatalog;

        private WearablesDTOList defaultWearablesDTOs;
        private GameObject defaultEmptyWearableAsset;

        public WearablePlugin(IAssetsProvisioner assetsProvisioner, IWebRequestController webRequestController, IRealmData realmData, URLDomain assetBundleURL, CacheCleaner cacheCleaner, IWearableCatalog wearableCatalog)
        {
            this.wearableCatalog = wearableCatalog;
            this.assetsProvisioner = assetsProvisioner;
            this.webRequestController = webRequestController;
            this.realmData = realmData;
            this.assetBundleURL = assetBundleURL;

            cacheCleaner.Register(this.wearableCatalog);
        }

        public void Dispose() { }

        public async UniTask InitializeAsync(WearableSettings settings, CancellationToken ct)
        {
            ProvidedAsset<TextAsset> defaultWearableDefinition = await assetsProvisioner.ProvideMainAssetAsync(settings.defaultWearablesDefinition, ct: ct);
            var partialTargetList = new List<WearableDTO>(64);
            JsonConvert.PopulateObject(defaultWearableDefinition.Value.text, partialTargetList);

            defaultWearablesDTOs = new WearablesDTOList(partialTargetList);

            var defaultEmptyWearable =
                await assetsProvisioner.ProvideMainAssetAsync(settings.defaultEmptyWearable, ct: ct);
            defaultEmptyWearableAsset = defaultEmptyWearable.Value;
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<World> builder, in GlobalPluginArguments arguments)
        {
            ResolveWearableByPointerSystem.InjectToWorld(ref builder, wearableCatalog, realmData, WEARABLES_EMBEDDED_SUBDIRECTORY, assetBundleURL);
            LoadWearablesByParamSystem.InjectToWorld(ref builder, webRequestController, new NoCache<WearablesResponse, GetWearableByParamIntention>(false, false), realmData, EXPLORER_SUBDIRECTORY, WEARABLES_COMPLEMENT_URL, wearableCatalog, assetBundleURL);
            LoadWearablesDTOByPointersSystem.InjectToWorld(ref builder, webRequestController, new NoCache<WearablesDTOList, GetWearableDTOByPointersIntention>(false, false));
            LoadWearableAssetBundleManifestSystem.InjectToWorld(ref builder, new NoCache<SceneAssetBundleManifest, GetWearableAssetBundleManifestIntention>(true, true), assetBundleURL);
            LoadDefaultWearablesSystem.InjectToWorld(ref builder, defaultWearablesDTOs, defaultEmptyWearableAsset,
                wearableCatalog);

            ResolveAvatarAttachmentThumbnailSystem.InjectToWorld(ref builder);
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
