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
using ECS.StreamableLoading.AssetBundles;
using ECS.StreamableLoading.Cache;
using ECS.StreamableLoading.Common.Components;
using Newtonsoft.Json;
using SceneRunner.Scene;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using UnityEngine;
using UnityEngine.Networking;
using Utility.Multithreading;

namespace DCL.AvatarRendering.Wearables
{
    public class WearablePlugin : IDCLGlobalPlugin<WearablePlugin.WearableSettings>
    {
        [Serializable]
        public class WearableSettings : IDCLPluginSettings
        {
            [field: SerializeField]
            public AssetReferenceTextAsset defaultWearablesDefinition;
        }

        //Should be taken from the catalyst
        private static readonly URLSubdirectory EXPLORER_SUBDIRECTORY = URLSubdirectory.FromString("/explorer/");
        private static readonly URLSubdirectory WEARABLES_COMPLEMENT_URL = URLSubdirectory.FromString("/wearables/");

        private readonly IRealmData realmData;
        private readonly URLDomain assetBundleURL;
        private readonly IAssetsProvisioner assetsProvisioner;
        private readonly WearableCatalog wearableCatalog;

        public async UniTask Initialize(WearableSettings settings, CancellationToken ct)
        {
            ProvidedAsset<TextAsset> defaultWearableDefinition = await assetsProvisioner.ProvideMainAsset(settings.defaultWearablesDefinition, ct: ct);
            var partialTargetList = new List<WearableDTO>();
            JsonConvert.PopulateObject(defaultWearableDefinition.Value.text, partialTargetList);

            foreach (WearableDTO wearableDto in partialTargetList)
            {
                wearableCatalog.AddWearableByDTO(wearableDto, out IWearable defaultWearable);
                BodyShape analyzedBodyShape = defaultWearable.IsCompatibleWithBodyShape(BodyShape.MALE) ? BodyShape.MALE : BodyShape.FEMALE;

                //Get main asset bundle
                UnityWebRequest assetBundleWebRequest = UnityWebRequestAssetBundle.GetAssetBundle($"file://{Application.streamingAssetsPath}/AssetBundles/Wearables/{defaultWearable.GetMainFileHash(analyzedBodyShape)}{PlatformUtils.GetPlatform()}");
                await assetBundleWebRequest.SendWebRequest();

                //Get dependencies
                AssetBundle assetBundle = DownloadHandlerAssetBundle.GetContent(assetBundleWebRequest);
                TextAsset metadata = assetBundle.LoadAsset<TextAsset>("metadata.json");
                AssetBundleMetadata assetBundleMetadata = JsonConvert.DeserializeObject<AssetBundleMetadata>(metadata.text);
                var assetBundlesDependencies = new List<AssetBundle>();

                foreach (string dependency in assetBundleMetadata.dependencies)
                {
                    UnityWebRequest dependencyWebRequest
                        = UnityWebRequestAssetBundle.GetAssetBundle($"file://{Application.streamingAssetsPath}/AssetBundles/Wearables/{dependency}");

                    await dependencyWebRequest.SendWebRequest();
                    assetBundlesDependencies.Add(DownloadHandlerAssetBundle.GetContent(dependencyWebRequest));
                }

                //Load asset bundle
                AssetBundleRequest asyncOp = assetBundle.LoadAllAssetsAsync<GameObject>();
                await asyncOp.WithCancellation(ct);
                var gameObjects = new List<GameObject>(asyncOp.allAssets.Cast<GameObject>());

                if (gameObjects.Count == 0)
                    continue;

                var assetBundleData
                    = new StreamableLoadingResult<AssetBundleData>(new AssetBundleData(assetBundle, null, gameObjects[0]));

                if (defaultWearable.IsUnisex())
                {
                    defaultWearable.AssetBundleData[BodyShape.MALE] = assetBundleData;
                    defaultWearable.AssetBundleData[BodyShape.FEMALE] = assetBundleData;
                }
                else
                    defaultWearable.AssetBundleData[analyzedBodyShape] = assetBundleData;

                //DUMMY ASSET BUNDLE MANIFEST
                defaultWearable.ManifestResult = new StreamableLoadingResult<SceneAssetBundleManifest>();

                assetBundle.Unload(false);

                foreach (AssetBundle assetBundlesDependency in assetBundlesDependencies) { assetBundlesDependency.Unload(false); }
            }
        }

        public WearablePlugin(IAssetsProvisioner assetsProvisioner, IRealmData realmData, URLDomain assetBundleURL)
        {
            wearableCatalog = new WearableCatalog();
            this.assetsProvisioner = assetsProvisioner;
            this.realmData = realmData;
            this.assetBundleURL = assetBundleURL;
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<World> builder, in GlobalPluginArguments arguments)
        {
            // not synced by mutex, for compatibility only
            var mutexSync = new MutexSync();

            ResolveWearableByPointerSystem.InjectToWorld(ref builder, wearableCatalog, realmData);
            LoadWearablesByParamSystem.InjectToWorld(ref builder, new NoCache<IWearable[], GetWearableByParamIntention>(false, false), realmData, EXPLORER_SUBDIRECTORY, WEARABLES_COMPLEMENT_URL, wearableCatalog, mutexSync);
            LoadWearablesDTOByPointersSystem.InjectToWorld(ref builder, new NoCache<WearableDTO[], GetWearableDTOByPointersIntention>(false, false), mutexSync);
            LoadWearableAssetBundleManifestSystem.InjectToWorld(ref builder, new NoCache<SceneAssetBundleManifest, GetWearableAssetBundleManifestIntention>(true, true), mutexSync, assetBundleURL);
        }

        public void Dispose() { }


    }
}
