using Arch.Core;
using AssetManagement;
using CommunicationData.URLHelpers;
using DCL.AvatarRendering.Wearables.Components;
using DCL.AvatarRendering.Wearables.Components.Intentions;
using DCL.Optimization.Pools;
using ECS.Prioritization.Components;
using ECS.StreamableLoading.AssetBundles;
using ECS.StreamableLoading.Common.Components;
using SceneRunner.Scene;
using System;
using System.Collections.Generic;
using UnityEngine;
using Utility;
using AssetBundlePromise = ECS.StreamableLoading.Common.AssetPromise<ECS.StreamableLoading.AssetBundles.AssetBundleData, ECS.StreamableLoading.AssetBundles.GetAssetBundleIntention>;

namespace DCL.AvatarRendering.Wearables.Helpers
{
    /// <summary>
    ///     Utilities to control <see cref="IWearable" /> based on the type of the underlying
    /// </summary>
    public static class WearablePolymorphicBehaviour
    {
        public const int MAIN_ASSET_INDEX = 0;
        public const int MASK_ASSET_INDEX = 1;

        /// <summary>
        ///     Create a certain number of AssetBundlePromises based on the type of the wearable,
        ///     if promises are already created does nothing and returns false
        /// </summary>
        public static bool TryCreateAssetBundlePromise(
            this IWearable wearable,
            in GetWearablesByPointersIntention intention,
            URLSubdirectory customStreamingSubdirectory,
            IPartitionComponent partitionComponent,
            World world)
        {
            SceneAssetBundleManifest manifest = !EnumUtils.HasFlag(intention.PermittedSources, AssetSource.WEB) ? null : wearable.ManifestResult?.Asset;

            var bodyShape = intention.BodyShape;

            switch (wearable.Type)
            {
                case WearableType.FacialFeature:
                    return TryCreateFacialFeaturePromises(
                        manifest,
                        in intention,
                        customStreamingSubdirectory,
                        wearable,
                        partitionComponent,
                        bodyShape,
                        world);
                default:
                    return TryCreateSingleGameObjectAssetBundlePromise(
                        manifest,
                        in intention,
                        customStreamingSubdirectory,
                        wearable,
                        partitionComponent,
                        bodyShape,
                        world);
            }
        }

        private static bool TryCreateSingleGameObjectAssetBundlePromise(
            SceneAssetBundleManifest sceneAssetBundleManifest,
            in GetWearablesByPointersIntention intention,
            URLSubdirectory customStreamingSubdirectory,
            IWearable wearable,
            IPartitionComponent partitionComponent,
            BodyShape bodyShape,
            World world)
        {
            ref var wearableAssets = ref InitializeResultsArray(wearable, bodyShape, 1);

            return TryCreateMainFilePromise(typeof(GameObject), sceneAssetBundleManifest, intention, customStreamingSubdirectory, wearable, partitionComponent, ref wearableAssets, bodyShape, world);
        }

        /// <summary>
        ///     Facial feature can consists of the main texture and the mask
        /// </summary>
        private static bool TryCreateFacialFeaturePromises(
            SceneAssetBundleManifest sceneAssetBundleManifest,
            in GetWearablesByPointersIntention intention,
            URLSubdirectory customStreamingSubdirectory,
            IWearable wearable,
            IPartitionComponent partitionComponent,
            BodyShape bodyShape,
            World world)
        {
            ref var wearableAssets = ref InitializeResultsArray(wearable, bodyShape, 2);

            // 0 stands for the main texture
            // 1 stands for the mask
            return TryCreateMainFilePromise(typeof(Texture), sceneAssetBundleManifest, intention, customStreamingSubdirectory, wearable, partitionComponent, ref wearableAssets, bodyShape, world)
                   | TryCreateMaskPromise(sceneAssetBundleManifest, intention, customStreamingSubdirectory, wearable, partitionComponent, ref wearableAssets, bodyShape, world);
        }

        private static ref WearableAssets InitializeResultsArray(IWearable wearable, BodyShape bodyShape, int size)
        {
            if (wearable.IsUnisex())
            {
                SetByRef(BodyShape.MALE);
                SetByRef(BodyShape.FEMALE);
            }
            else
                SetByRef(bodyShape);

            return ref wearable.WearableAssetResults[bodyShape];

            void SetByRef(BodyShape bs)
            {
                ref WearableAssets resultForBody = ref wearable.WearableAssetResults[bs];
                resultForBody.Results ??= new StreamableLoadingResult<WearableAssetBase>?[size];
            }
        }

        private static bool TryCreateMaskPromise(SceneAssetBundleManifest sceneAssetBundleManifest,
            GetWearablesByPointersIntention intention, URLSubdirectory customStreamingSubdirectory, IWearable wearable,
            IPartitionComponent partitionComponent, ref WearableAssets wearableAssets, BodyShape bodyShape, World world)
        {
            if (wearableAssets.Results[MASK_ASSET_INDEX] != null)
                return false;

            if (!wearable.TryGetFileHashConditional(bodyShape,
                    static content => content.EndsWith("_mask.png", StringComparison.OrdinalIgnoreCase),
                    out string mainFileHash))
            {
                // If there is no mask, we don't need to create a promise for it, and it's not an exception
                wearableAssets.Results[MASK_ASSET_INDEX] = new StreamableLoadingResult<WearableAssetBase>((WearableTextureAsset) null);
                return false;
            }

            CreatePromise(
                typeof(Texture),
                sceneAssetBundleManifest,
                intention,
                customStreamingSubdirectory,
                mainFileHash,
                wearable,
                MASK_ASSET_INDEX,
                partitionComponent,
                world);

            return true;
        }

        private static bool TryCreateMainFilePromise(
            Type expectedObjectType,
            SceneAssetBundleManifest sceneAssetBundleManifest,
            GetWearablesByPointersIntention intention, URLSubdirectory customStreamingSubdirectory, IWearable wearable,
            IPartitionComponent partitionComponent, ref WearableAssets wearableAssets, BodyShape bodyShape, World world)
        {
            if (wearableAssets.Results[MAIN_ASSET_INDEX] != null)
                return false;

            if (!wearable.TryGetMainFileHash(bodyShape, out string mainFileHash))
            {
                wearableAssets.Results[MAIN_ASSET_INDEX] =
                    new StreamableLoadingResult<WearableAssetBase>(new Exception("Main file hash not found"));

                return false;
            }

            CreatePromise(expectedObjectType,
                sceneAssetBundleManifest,
                intention,
                customStreamingSubdirectory,
                mainFileHash,
                wearable,
                MAIN_ASSET_INDEX,
                partitionComponent,
                world);

            return true;
        }

        private static void CreatePromise(
            Type expectedObjectType,
            SceneAssetBundleManifest sceneAssetBundleManifest,
            GetWearablesByPointersIntention intention,
            URLSubdirectory customStreamingSubdirectory,
            string hash,
            IWearable wearable,
            int index,
            IPartitionComponent partitionComponent,
            World world)
        {
            var promise = AssetBundlePromise.Create(world,
                GetAssetBundleIntention.FromHash(
                    expectedObjectType,
                    hash + PlatformUtils.GetPlatform(),
                    permittedSources: intention.PermittedSources,
                    customEmbeddedSubDirectory: customStreamingSubdirectory,
                    manifest: sceneAssetBundleManifest, cancellationTokenSource: intention.CancellationTokenSource),
                partitionComponent);

            wearable.IsLoading = true;
            world.Create(promise, wearable, intention.BodyShape, index); // Add an index to the promise so we know to which slot of the WearableAssets it belongs
        }

        public static StreamableLoadingResult<WearableAssetBase> ToWearableAsset(this StreamableLoadingResult<AssetBundleData> result, IWearable wearable)
        {
            if (!result.Succeeded) return new StreamableLoadingResult<WearableAssetBase>(result.Exception!);

            switch (wearable.Type)
            {
                case WearableType.FacialFeature:
                    return new StreamableLoadingResult<WearableAssetBase>(new WearableTextureAsset(result.Asset!.GetMainAsset<Texture>(), result.Asset));
                default:
                {
                    var go = result.Asset!.GetMainAsset<GameObject>();
                    // collect all renderers
                    List<WearableRegularAsset.RendererInfo> rendererInfos = WearableRegularAsset.RENDERER_INFO_POOL.Get();

                    using PoolExtensions.Scope<List<SkinnedMeshRenderer>> pooledList = result.Asset.GetMainAsset<GameObject>().GetComponentsInChildrenIntoPooledList<SkinnedMeshRenderer>();

                    foreach (SkinnedMeshRenderer skinnedMeshRenderer in pooledList.Value)
                        rendererInfos.Add(new WearableRegularAsset.RendererInfo(skinnedMeshRenderer, skinnedMeshRenderer.sharedMaterial));

                    return new StreamableLoadingResult<WearableAssetBase>(new WearableRegularAsset(go, rendererInfos, result.Asset));
                }
            }
        }

        public static void AssignWearableAsset(this IWearable wearable, WearableRegularAsset wearableRegularAsset, BodyShape bodyShape)
        {
            ref var results = ref wearable.WearableAssetResults[bodyShape];
            results.Results ??= new StreamableLoadingResult<WearableAssetBase>?[1];

            results.Results[MAIN_ASSET_INDEX] = new StreamableLoadingResult<WearableAssetBase>(wearableRegularAsset);
        }
    }
}
