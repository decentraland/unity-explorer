using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using DCL.Diagnostics;
using DCL.LOD.Components;
using ECS.Abstract;
using ECS.SceneLifeCycle;
using ECS.StreamableLoading.AssetBundles;
using ECS.StreamableLoading.Common.Components;
using ECS.Unity.GLTFContainer;
using ECS.Unity.GLTFContainer.Asset.Cache;
using ECS.Unity.GLTFContainer.Asset.Components;
using System.Collections.Generic;
using UnityEngine;
using AssetBundlePromise = ECS.StreamableLoading.Common.AssetPromise<ECS.StreamableLoading.AssetBundles.AssetBundleData, ECS.StreamableLoading.AssetBundles.GetAssetBundleIntention>;

namespace DCL.LOD.Systems
{
    [UpdateInGroup(typeof(RealmGroup))]
    [UpdateBefore(typeof(InstantiateSceneLODInfoSystem))]
    [LogCategory(ReportCategory.LOD)]
    public partial class ResolveISSLODSystem : BaseUnityLoopSystem
    {

        private IGltfContainerAssetsCache gltfCache;
        private InitialSceneStateLOD initialSceneStateLOD;

        public ResolveISSLODSystem(World world, IGltfContainerAssetsCache gltfCache) : base(world)
        {
            this.gltfCache = gltfCache;
        }

        protected override void Update(float t)
        {
            ResolveInitialSceneStateLODQuery(World);
        }

        [Query]
        private void ResolveInitialSceneStateLOD(ref SceneLODInfo sceneLODInfo)
        {
            initialSceneStateLOD = sceneLODInfo.InitialSceneStateLOD;

            if (initialSceneStateLOD.Failed)
                return;

            if (initialSceneStateLOD.Processing)
            {
                // Skip if promise hasn't been created yet or is already consumed
                if (initialSceneStateLOD.AssetBundlePromise == AssetBundlePromise.NULL || initialSceneStateLOD.AssetBundlePromise.IsConsumed) return;

                if (initialSceneStateLOD.AssetBundlePromise.TryConsume(World, out StreamableLoadingResult<AssetBundleData> Result))
                {
                    if (Result.Succeeded)
                    {
                        if (Result.Asset!.InitialSceneStateMetadata.HasValue)
                        {
                            GameObject lodParent = new GameObject($"{sceneLODInfo.id}_ISS_LOD");
                            InitialSceneStateMetadata initialSceneStateMetadata = Result.Asset!.InitialSceneStateMetadata.Value;
                            //TODO (JUANI) : Pool?
                            List<(string, GltfContainerAsset)> containiningAssets = new List<(string, GltfContainerAsset)>();

                            //TODO (JUANI) : Budgeting
                            for (var i = 0; i < initialSceneStateMetadata.assetHash.Count; i++)
                            {
                                string assetHash = initialSceneStateMetadata.assetHash[i];
                                if (gltfCache.TryGet(assetHash, out var asset))
                                    asset.Root.SetActive(true);
                                else
                                {
                                    asset = Utils.CreateGltfObject(Result.Asset, assetHash);
                                    //TODO (JUANI) : Manually adding reference since we are not going trough the AB system
                                    Result.Asset!.AddReference();
                                }

                                asset.Root.transform.SetParent(lodParent.transform);
                                asset.Root.transform.position = initialSceneStateMetadata.positions[i];
                                asset.Root.transform.rotation = initialSceneStateMetadata.rotations[i];
                                asset.Root.transform.localScale = initialSceneStateMetadata.scales[i];


                                containiningAssets.Add((assetHash, asset));
                            }

                            initialSceneStateLOD.Result = lodParent;
                            initialSceneStateLOD.Resolved = true;
                            initialSceneStateLOD.AssetBundleData = Result.Asset;
                            initialSceneStateLOD.Assets = containiningAssets;
                            initialSceneStateLOD.gltfCache = gltfCache;
                        }
                        else
                        {
                            MarkAssetBundleAsFailed(ref sceneLODInfo,
                                $"No initial scene state descriptor in the ISS for {sceneLODInfo.id}, will try to do the old LOD");
                            initialSceneStateLOD.AssetBundleData!.Dispose();
                        }
                    }
                    else
                    {
                        MarkAssetBundleAsFailed(ref sceneLODInfo,
                            $"Failed to get ISS LOD for  {sceneLODInfo.id}, will try to do the old LOD");
                    }
                }
            }
        }

        private static void MarkAssetBundleAsFailed(ref SceneLODInfo sceneLODInfo, string message)
        {
            ReportHub.Log(ReportCategory.LOD, message);
            sceneLODInfo.InitialSceneStateLOD.Failed = true;
            sceneLODInfo.InitialSceneStateLOD.Resolved = true;
        }

    }
}
