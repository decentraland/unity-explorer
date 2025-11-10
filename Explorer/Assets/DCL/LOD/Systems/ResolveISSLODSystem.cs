using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using DCL.Diagnostics;
using DCL.LOD.Components;
using DCL.Optimization.PerformanceBudgeting;
using DCL.PluginSystem;
using ECS.Abstract;
using ECS.Prioritization.Components;
using ECS.SceneLifeCycle;
using ECS.SceneLifeCycle.SceneDefinition;
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
        private readonly IPerformanceBudget instantiationFrameTimeBudget;
        private readonly IPerformanceBudget memoryBudget;

        public ResolveISSLODSystem(World world, IGltfContainerAssetsCache gltfCache, IPerformanceBudget instantiationFrameTimeBudget, IPerformanceBudget memoryBudget) : base(world)
        {
            this.gltfCache = gltfCache;
            this.instantiationFrameTimeBudget = instantiationFrameTimeBudget;
            this.memoryBudget = memoryBudget;
        }

        protected override void Update(float t)
        {
            ResolveInitialSceneStateLODQuery(World);
            ConvertFromAssetBundleQuery(World);
        }

        [Query]
        private void ResolveInitialSceneStateLOD(ref SceneLODInfo sceneLODInfo, ref SceneDefinitionComponent sceneDefinition)
        {
            InitialSceneStateLOD initialSceneStateLOD = sceneLODInfo.InitialSceneStateLOD;

            if (initialSceneStateLOD.CurrentState == InitialSceneStateLOD.InitialSceneStateLODState.PROCESSING)
            {
                // Skip if promise hasn't been created yet or is already consumed
                if (initialSceneStateLOD.AssetBundlePromise == AssetBundlePromise.NULL || initialSceneStateLOD.AssetBundlePromise.IsConsumed) return;

                if (initialSceneStateLOD.AssetBundlePromise.TryConsume(World, out StreamableLoadingResult<AssetBundleData> Result))
                {
                    if (Result.Succeeded)
                    {
                        if (Result.Asset!.InitialSceneStateMetadata.HasValue)
                        {
                            InitialSceneStateMetadata initialSceneStateMetadata = Result.Asset!.InitialSceneStateMetadata.Value;
                            initialSceneStateLOD.Initialize(sceneLODInfo.id, sceneDefinition.SceneGeometry.BaseParcelPosition, Result.Asset,
                                gltfCache, initialSceneStateMetadata.assetHash.Count);

                            for (var i = 0; i < initialSceneStateMetadata.assetHash.Count; i++)
                            {
                                string assetHash = initialSceneStateMetadata.assetHash[i];

                                if (gltfCache.TryGet(assetHash, out var asset))
                                    PositionAsset(initialSceneStateLOD, assetHash, asset, initialSceneStateLOD.ParentContainer.transform, initialSceneStateMetadata, i);
                                else
                                {
                                    //Little bit redundant, but needed for correct ref counting
                                    AssetBundlePromise promise = AssetBundlePromise.Create(World,
                                        GetAssetBundleIntention.FromHash(GetAssetBundleIntention.BuildInitialSceneStateURL(sceneDefinition.Definition.id),
                                            assetBundleManifestVersion: sceneDefinition.Definition.assetBundleManifestVersion,
                                            parentEntityID: sceneDefinition.Definition.id),
                                        PartitionComponent.TOP_PRIORITY);

                                    ISSAssetCreationHelper assetCreationHelper
                                        = new ISSAssetCreationHelper(initialSceneStateLOD, assetHash, i);

                                    World.Create(promise, assetCreationHelper);
                                }
                            }
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


        [Query]
        private void ConvertFromAssetBundle(in Entity entity, ISSAssetCreationHelper creationHelper, ref AssetBundlePromise assetBundleResult)
        {
            if (!instantiationFrameTimeBudget.TrySpendBudget() || !memoryBudget.TrySpendBudget())
                return;

            if (assetBundleResult.TryConsume(World, out StreamableLoadingResult<AssetBundleData> Result))
            {
                if (Result.Succeeded)
                {
                    GltfContainerAsset asset = Utils.CreateGltfObject(Result.Asset, creationHelper.AssetHash);
                    PositionAsset(creationHelper.InitialSceneStateLOD, creationHelper.AssetHash, asset, creationHelper.InitialSceneStateLOD.ParentContainer.transform, Result.Asset.InitialSceneStateMetadata.Value, creationHelper.IndexToCreate);
                }
                World.Destroy(entity);
            }
        }


        private void PositionAsset(InitialSceneStateLOD initialSceneStateLOD, string assetHash, GltfContainerAsset asset, Transform parent, InitialSceneStateMetadata initialSceneStateMetadata, int indexToPosition)
        {
            asset.Root.SetActive(true);
            asset.Root.transform.SetParent(parent);
            asset.Root.transform.localPosition = initialSceneStateMetadata.positions[indexToPosition];
            asset.Root.transform.localRotation = initialSceneStateMetadata.rotations[indexToPosition];
            asset.Root.transform.localScale = initialSceneStateMetadata.scales[indexToPosition];

            foreach (Animation assetAnimation in asset.Animations)
                assetAnimation.enabled = false;

            foreach (Animator assetAnimator in asset.Animators)
                assetAnimator.enabled = false;

            initialSceneStateLOD.AddResolvedAsset(assetHash, asset);
        }

        private static void MarkAssetBundleAsFailed(ref SceneLODInfo sceneLODInfo, string message)
        {
            ReportHub.Log(ReportCategory.LOD, message);
            sceneLODInfo.InitialSceneStateLOD.CurrentState = InitialSceneStateLOD.InitialSceneStateLODState.FAILED;
            //We need to re-evaluate the LOD to see if we can get the old method
            sceneLODInfo.CurrentLODLevelPromise = byte.MaxValue;
        }

    }

    public struct ISSAssetCreationHelper
    {
        public ISSAssetCreationHelper(InitialSceneStateLOD initialSceneStateLOD, string assetHash, int indexToCreate)
        {
            InitialSceneStateLOD = initialSceneStateLOD;
            AssetHash = assetHash;
            IndexToCreate = indexToCreate;
        }

        public InitialSceneStateLOD InitialSceneStateLOD { get;  }
        public string AssetHash { get;  }
        public int IndexToCreate { get; }
    }
}
