using Arch.Core;
using Arch.SystemGroups;
using DCL.AvatarRendering.Wearables.Components;
using DCL.AvatarRendering.Wearables.Helpers;
using DCL.AvatarRendering.Wearables.Systems;
using DCL.PluginSystem.Global;
using ECS.StreamableLoading.AssetBundles;
using ECS.StreamableLoading.Cache;
using ECS.StreamableLoading.DeferredLoading.BudgetProvider;
using SceneRunner.Scene;
using UnityEngine;
using Utility.Multithreading;

namespace DCL.AvatarRendering.Wearables
{
    public class WearablePlugin : IDCLGlobalPluginWithoutSettings
    {
        public static readonly string STREAMING_ASSETS_URL =
#if UNITY_EDITOR || UNITY_STANDALONE
            $"file://{Application.streamingAssetsPath}/AssetBundles/";
#else
            return $"{Application.streamingAssetsPath}/AssetBundles/";
#endif

        //Should be taken from the catalyst
        public readonly string AB_ASSETS_URL = "https://ab-cdn.decentraland.org/";
        public readonly string EXPLORER_LAMBDA_URL = "https://peer-ec1.decentraland.org/explorer";
        public readonly string CONTENT_URL = "https://peer-ec1.decentraland.org/content";


        public void InjectToWorld(ref ArchSystemsWorldBuilder<World> builder, in GlobalPluginArguments arguments)
        {
            // not synced by mutex, for compatibility only
            var mutexSync = new MutexSync();

            WearableDeferredLoadingSystem.InjectToWorld(ref builder, new ConcurrentLoadingBudgetProvider(50));

            LoadWearablesByParamSystem.InjectToWorld(ref builder, new NoCache<WearableDTO[], GetWearableByParamIntention>(), mutexSync, EXPLORER_LAMBDA_URL);
            LoadWearablesByPointersSystem.InjectToWorld(ref builder, new WearablesByPointersCache(), mutexSync);

            LoadWearableSystem.InjectToWorld(ref builder, CONTENT_URL);
            LoadWearableAssetBundleManifestSystem.InjectToWorld(ref builder, new NoCache<SceneAssetBundleManifest, GetWearableAssetBundleManifestIntention>(), mutexSync, AB_ASSETS_URL);
            PrepareWearableAssetBundleLoadingParametersSystem.InjectToWorld(ref builder, STREAMING_ASSETS_URL);
            LoadWearableAssetBundleSystem.InjectToWorld(ref builder, new WearableAssetBundleCache(), mutexSync, new AssetBundleLoadingMutex());
        }
    }
}
