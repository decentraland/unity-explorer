using Arch.Core;
using Arch.SystemGroups;
using ECS.LifeCycle;
using ECS.StreamableLoading.AssetBundles;
using ECS.StreamableLoading.AssetBundles.Manifest;
using ECS.StreamableLoading.Cache;
using System.Collections.Generic;
using UnityEngine;

namespace SceneRunner.ECSWorld.Plugins
{
    public class AssetBundlesPlugin : IECSWorldPlugin
    {
        private static readonly string STREAMING_ASSETS_URL =
#if UNITY_EDITOR || UNITY_STANDALONE
            $"file://{Application.streamingAssetsPath}/AssetBundles/";
#else
            return $"{Application.streamingAssetsPath}/AssetBundles/";
#endif

        private readonly AssetBundlesManifestCache assetBundlesManifestCache;

        public void InjectToWorld(ref ArchSystemsWorldBuilder<World> builder, in ECSWorldInstanceSharedDependencies sharedDependencies, List<IFinalizeWorldSystem> finalizeWorldSystems)
        {
            // Asset Bundles
            PrepareAssetBundleLoadingParametersSystem.InjectToWorld(ref builder, sharedDependencies.SceneData, STREAMING_ASSETS_URL);

            // TODO create a runtime ref-counting cache
            LoadAssetBundleSystem.InjectToWorld(ref builder, NoCache<AssetBundleData, GetAssetBundleIntention>.INSTANCE);
        }
    }
}
