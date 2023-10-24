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
using ECS;
using ECS.StreamableLoading.Cache;
using ECS.StreamableLoading.DeferredLoading.BudgetProvider;
using Newtonsoft.Json;
using SceneRunner.Scene;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
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
        private readonly MemoryBudgetProvider memoryBudgetProvider;

        private readonly IRealmData realmData;
        private readonly WearableCatalog wearableCatalog;

        private WearablesDTOList defaultWearablesDTOs;

        public WearablePlugin(IAssetsProvisioner assetsProvisioner, MemoryBudgetProvider memoryBudgetProvider, IRealmData realmData, URLDomain assetBundleURL)
        {
            wearableCatalog = new WearableCatalog();
            this.assetsProvisioner = assetsProvisioner;
            this.memoryBudgetProvider = memoryBudgetProvider;
            this.realmData = realmData;
            this.assetBundleURL = assetBundleURL;
        }

        public void Dispose() { }

        public async UniTask InitializeAsync(WearableSettings settings, CancellationToken ct)
        {
            ProvidedAsset<TextAsset> defaultWearableDefinition = await assetsProvisioner.ProvideMainAssetAsync(settings.defaultWearablesDefinition, ct: ct);
            var partialTargetList = new List<WearableDTO>(64);
            JsonConvert.PopulateObject(defaultWearableDefinition.Value.text, partialTargetList);

            defaultWearablesDTOs = new WearablesDTOList(partialTargetList);
        }

        public void Dispose() { }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<World> builder, in GlobalPluginArguments arguments)
        {
            // not synced by mutex, for compatibility only
            var mutexSync = new MutexSync();

            ResolveWearableByPointerSystem.InjectToWorld(ref builder, wearableCatalog, realmData, WEARABLES_EMBEDDED_SUBDIRECTORY);
            LoadWearablesByParamSystem.InjectToWorld(ref builder, new NoCache<IWearable[], GetWearableByParamIntention>(false, false), memoryBudgetProvider, realmData, EXPLORER_SUBDIRECTORY, WEARABLES_COMPLEMENT_URL, wearableCatalog, mutexSync);
            LoadWearablesDTOByPointersSystem.InjectToWorld(ref builder, memoryBudgetProvider, new NoCache<WearablesDTOList, GetWearableDTOByPointersIntention>(false, false), mutexSync);
            LoadWearableAssetBundleManifestSystem.InjectToWorld(ref builder, memoryBudgetProvider, new NoCache<SceneAssetBundleManifest, GetWearableAssetBundleManifestIntention>(true, true), mutexSync, assetBundleURL);
            LoadDefaultWearablesSystem.InjectToWorld(ref builder, defaultWearablesDTOs, wearableCatalog);
        }

        [Serializable]
        public class WearableSettings : IDCLPluginSettings
        {
            [field: SerializeField]
            public AssetReferenceTextAsset defaultWearablesDefinition;
        }
    }
}
