using Arch.Core;
using Arch.SystemGroups;
using DCL.AvatarRendering.Wearables.Components;
using DCL.AvatarRendering.Wearables.Systems;
using DCL.PluginSystem.Global;
using ECS.StreamableLoading.AssetBundles;
using ECS.StreamableLoading.Cache;
using ECS.StreamableLoading.DeferredLoading.BudgetProvider;
using UnityEngine;
using Utility.Multithreading;

namespace DCL.AvatarRendering.Wearables
{
    public class WearablePlugin : IDCLGlobalPluginWithoutSettings
    {
        private readonly WearableDTOCache wearableDTOCache;
        public static readonly string STREAMING_ASSETS_URL =
#if UNITY_EDITOR || UNITY_STANDALONE
            $"file://{Application.streamingAssetsPath}/AssetBundles/";
#else
            return $"{Application.streamingAssetsPath}/AssetBundles/";
#endif

        public readonly string AB_ASSETS_URL = "https://ab-cdn.decentraland.org/";

        public WearablePlugin()
        {
            //TODO: Rethink the cache system
            wearableDTOCache = new WearableDTOCache();
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<World> builder, in GlobalPluginArguments arguments)
        {
            // not synced by mutex, for compatibility only
            var mutexSync = new MutexSync();
            var assetBundleLoadingMutex = new AssetBundleLoadingMutex();

            LoadWearableSystem.InjectToWorld(ref builder);
            WearableDeferredLoadingSystem.InjectToWorld(ref builder, new ConcurrentLoadingBudgetProvider(50));

            LoadWearablesDTOSystem.InjectToWorld(ref builder, wearableDTOCache, mutexSync, AB_ASSETS_URL);
            PrepareWearableAssetBundleLoadingParametersSystem.InjectToWorld(ref builder, STREAMING_ASSETS_URL);

            LoadWearableAssetBundleSystem.InjectToWorld(ref builder, new NoCache<AssetBundleData, GetWearableAssetBundleIntention>(),
                mutexSync, assetBundleLoadingMutex);
        }
    }
}
