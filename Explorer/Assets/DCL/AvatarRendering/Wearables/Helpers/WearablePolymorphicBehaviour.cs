using Arch.Core;
using AssetManagement;
using CommunicationData.URLHelpers;
using DCL.AvatarRendering.Loading.Assets;
using DCL.AvatarRendering.Loading.Components;
using DCL.AvatarRendering.Loading.DTO;
using DCL.AvatarRendering.Wearables.Components;
using DCL.AvatarRendering.Wearables.Components.Intentions;
using DCL.Diagnostics;
using DCL.Optimization.Pools;
using ECS.Prioritization.Components;
using ECS.StreamableLoading.AssetBundles;
using ECS.StreamableLoading.Common.Components;
using ECS.StreamableLoading.GLTF;
using ECS.StreamableLoading.Textures;
using SceneRunner.Scene;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using Utility;
using AssetBundlePromise = ECS.StreamableLoading.Common.AssetPromise<ECS.StreamableLoading.AssetBundles.AssetBundleData, ECS.StreamableLoading.AssetBundles.GetAssetBundleIntention>;
using AssetBundleManifestPromise = ECS.StreamableLoading.Common.AssetPromise<SceneRunner.Scene.SceneAssetBundleManifest, DCL.AvatarRendering.Wearables.Components.GetWearableAssetBundleManifestIntention>;
using RawGltfPromise = ECS.StreamableLoading.Common.AssetPromise<ECS.StreamableLoading.GLTF.GLTFData, ECS.StreamableLoading.GLTF.GetGLTFIntention>;
using TexturePromise = ECS.StreamableLoading.Common.AssetPromise<ECS.StreamableLoading.Textures.Texture2DData, ECS.StreamableLoading.Textures.GetTextureIntention>;
using IAvatarAttachment = DCL.AvatarRendering.Loading.Components.IAvatarAttachment;

namespace DCL.AvatarRendering.Wearables.Helpers
{
    /// <summary>
    ///     Utilities to control <see cref="IWearable" /> based on the type of the underlying
    /// </summary>
    public static class WearablePolymorphicBehaviour
    {
        public const int MAIN_ASSET_INDEX = 0;
        public const int MASK_ASSET_INDEX = 1;

        public static bool CreateAssetBundleManifestPromise<T>(this T component, World world, BodyShape bodyShape, CancellationTokenSource cts, IPartitionComponent partitionComponent)
            where T: IAvatarAttachment
        {
            var promise = AssetBundleManifestPromise.Create(world,
                new GetWearableAssetBundleManifestIntention(component.DTO.GetHash(), new CommonLoadingArguments(component.DTO.GetHash(), cancellationTokenSource: cts)),
                partitionComponent);

            component.ManifestResult = new StreamableLoadingResult<SceneAssetBundleManifest>();
            component.UpdateLoadingStatus(true);
            world.Create(promise, component, bodyShape);
            return true;
        }

        /// <summary>
        ///     Create a certain number of Asset Promises based on the type of the wearable,
        ///     if promises are already created does nothing and returns false
        /// </summary>
        public static bool TryCreateAssetPromise(
            this IWearable wearable,
            in GetWearablesByPointersIntention intention,
            URLSubdirectory customStreamingSubdirectory,
            IPartitionComponent partitionComponent,
            World world,
            ReportData reportData)
        {
            SceneAssetBundleManifest? manifest = !EnumUtils.HasFlag(intention.PermittedSources, AssetSource.WEB) ? null : wearable.ManifestResult?.Asset;

            BodyShape bodyShape = intention.BodyShape;

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
                        world,
                        reportData);
                default:
                    return TryCreateSingleGameObjectPromise(
                        manifest,
                        in intention,
                        customStreamingSubdirectory,
                        wearable,
                        partitionComponent,
                        bodyShape,
                        world,
                        reportData);
            }
        }

        public static bool TryCreateSingleGameObjectPromise(
            SceneAssetBundleManifest? sceneAssetBundleManifest,
            in GetWearablesByPointersIntention intention,
            URLSubdirectory customStreamingSubdirectory,
            IWearable wearable,
            IPartitionComponent partitionComponent,
            BodyShape bodyShape,
            World world,
            ReportData reportData)
        {
            ref WearableAssets wearableAssets = ref InitializeResultsArray(wearable, bodyShape, 1);

            return TryCreateMainFilePromise(typeof(GameObject), sceneAssetBundleManifest, intention, customStreamingSubdirectory, wearable, partitionComponent, ref wearableAssets, bodyShape, world, reportData);
        }

        /// <summary>
        ///     Facial feature can consist of the main texture and the mask
        /// </summary>
        private static bool TryCreateFacialFeaturePromises(
            SceneAssetBundleManifest? sceneAssetBundleManifest,
            in GetWearablesByPointersIntention intention,
            URLSubdirectory customStreamingSubdirectory,
            IWearable wearable,
            IPartitionComponent partitionComponent,
            BodyShape bodyShape,
            World world,
            ReportData reportData)
        {
            ref WearableAssets wearableAssets = ref InitializeResultsArray(wearable, bodyShape, 2);

            // 0 stands for the main texture
            // 1 stands for the mask
            return TryCreateMainFilePromise(typeof(Texture), sceneAssetBundleManifest, intention, customStreamingSubdirectory, wearable, partitionComponent, ref wearableAssets, bodyShape, world, reportData)
                   | TryCreateMaskPromise(sceneAssetBundleManifest, intention, customStreamingSubdirectory, wearable, partitionComponent, ref wearableAssets, bodyShape, world);
        }

        private static ref WearableAssets InitializeResultsArray(IWearable wearable, BodyShape bodyShape, int size)
        {
            if (wearable.IsUnisex() && wearable.HasSameModelsForAllGenders())
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
                resultForBody.Results ??= new StreamableLoadingResult<AttachmentAssetBase>?[size];
            }
        }

        private static bool TryCreateMaskPromise(SceneAssetBundleManifest?  sceneAssetBundleManifest,
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
                wearableAssets.Results[MASK_ASSET_INDEX] = new StreamableLoadingResult<AttachmentAssetBase>((AttachmentTextureAsset)null);
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

        private static bool TryCreateMainFilePromise<T>(
            Type expectedObjectType,
            SceneAssetBundleManifest? sceneAssetBundleManifest,
            GetWearablesByPointersIntention intention,
            URLSubdirectory customStreamingSubdirectory,
            T wearable,
            IPartitionComponent partitionComponent,
            ref WearableAssets wearableAssets,
            BodyShape bodyShape,
            World world,
            ReportData reportData
        )
            where T: IAvatarAttachment
        {
            if (wearableAssets.Results[MAIN_ASSET_INDEX] != null)
                return false;

            if (!wearable.TryGetMainFileHash(bodyShape, out string? mainFileHash))
            {
                wearableAssets.Results[MAIN_ASSET_INDEX] =
                    new StreamableLoadingResult<AttachmentAssetBase>(reportData, new Exception("Main file hash not found"));

                return false;
            }

            CreatePromise(expectedObjectType,
                sceneAssetBundleManifest,
                intention,
                customStreamingSubdirectory,
                mainFileHash!,
                wearable,
                MAIN_ASSET_INDEX,
                partitionComponent,
                world);

            return true;
        }

        private static void CreatePromise<T>(
            Type expectedObjectType,
            SceneAssetBundleManifest? sceneAssetBundleManifest,
            GetWearablesByPointersIntention intention,
            URLSubdirectory customStreamingSubdirectory,
            string hash,
            T wearable,
            int index,
            IPartitionComponent partitionComponent,
            World world) where T: IAvatarAttachment
        {
            if (!string.IsNullOrEmpty(wearable.DTO.ContentDownloadUrl))
            {
                Debug.Log($"PRAVS - WearablePolymorphicBehaviour.CreatePromise() - wearable.DTO.ContentDownloadUrl: {wearable.DTO.ContentDownloadUrl}");

                foreach (AvatarAttachmentDTO.Content content in wearable.DTO.content)
                {
                    if (content.hash == hash)
                    {
                        CreateRawWearablePromise(
                            content,
                            intention,
                            wearable,
                            index,
                            partitionComponent,
                            world);

                        break;
                    }
                }
            }
            else
            {
                // An index is added to the promise to know to which slot of the WearableAssets it belongs to
                var promise = AssetBundlePromise.Create(world,
                    GetAssetBundleIntention.FromHash(
                        expectedObjectType,
                        hash + PlatformUtils.GetCurrentPlatform(),
                        permittedSources: intention.PermittedSources,
                        customEmbeddedSubDirectory: customStreamingSubdirectory,
                        manifest: sceneAssetBundleManifest, cancellationTokenSource: intention.CancellationTokenSource),
                    partitionComponent);
                world.Create(promise, wearable, intention.BodyShape, index);
            }

            wearable.UpdateLoadingStatus(true);
        }

        /// <summary>
        ///     Handle the creation of non-asset-bundle wearable promises, either a GLTFData promise for regular
        ///     wearables or a Texture2DData promise for Facial Feature wearables.
        /// </summary>
        private static void CreateRawWearablePromise<T>(
            AvatarAttachmentDTO.Content content,
            GetWearablesByPointersIntention intention,
            T wearable,
            int index,
            IPartitionComponent partitionComponent,
            World world) where T: IAvatarAttachment
        {
            Debug.Log($"PRAVS - WearablePolymorphicBehaviour.CreateRawWearablePromise() - 1 - content.file: {content.file}");

            // An index is added to the promises to know to which slot of the WearableAssets it belongs to

            if (content.file.EndsWith(".glb")) // Wearables cannot be ".gltf"
            {
                Debug.Log($"PRAVS - WearablePolymorphicBehaviour.CreateRawWearablePromise() - 2");
                var promise = RawGltfPromise.Create(world, GetGLTFIntention.Create(content.file, content.hash), partitionComponent);
                world.Create(promise, wearable, intention.BodyShape, index);
            }
            else if (content.file.EndsWith(".png")) // Facial Feature Wearables documentation specifies PNG format
            {
                var promise = TexturePromise.Create(world,
                    new GetTextureIntention
                    {
                        // If cancellation token source was not provided a new one will be created
                        CommonArguments = new CommonLoadingArguments(URLAddress.FromString(wearable.DTO.ContentDownloadUrl+content.hash), cancellationTokenSource: intention.CancellationTokenSource),
                    },
                    partitionComponent);
                world.Create(promise, wearable, intention.BodyShape, index);
            }
        }

        // Asset Bundle Wearable
        public static StreamableLoadingResult<AttachmentAssetBase> ToWearableAsset(this StreamableLoadingResult<AssetBundleData> result, IWearable wearable)
        {
            if (!result.Succeeded) return new StreamableLoadingResult<AttachmentAssetBase>(result.ReportData, result.Exception!);

            switch (wearable.Type)
            {
                case WearableType.FacialFeature:
                    return new StreamableLoadingResult<AttachmentAssetBase>(new AttachmentTextureAsset(result.Asset!.GetMainAsset<Texture>(), result.Asset));
                default:
                    return new StreamableLoadingResult<AttachmentAssetBase>(ToRegularAsset(result));
            }
        }

        // Raw GLTF Wearable
        public static StreamableLoadingResult<AttachmentAssetBase> ToWearableAsset(this StreamableLoadingResult<GLTFData> result, IWearable wearable)
        {
            if (!result.Succeeded) return new StreamableLoadingResult<AttachmentAssetBase>(result.ReportData, result.Exception!);

            return new StreamableLoadingResult<AttachmentAssetBase>(ToRegularAsset(result));
        }

        // Raw Facial Feature Wearable
        public static StreamableLoadingResult<AttachmentAssetBase> ToWearableAsset(this StreamableLoadingResult<Texture2DData> result, IWearable wearable)
        {
            if (!result.Succeeded) return new StreamableLoadingResult<AttachmentAssetBase>(result.ReportData, result.Exception!);

            // Has to be RGBA32 to work with the avatar shader TextureArrays (Unity cannot compress BC7 in runtime)
            return new StreamableLoadingResult<AttachmentAssetBase>(new AttachmentTextureAsset(TextureUtilities.EnsureRGBA32Format(result.Asset!.Asset), result.Asset));
        }

        public static AttachmentRegularAsset ToRegularAsset(this StreamableLoadingResult<AssetBundleData> result)
        {
            GameObject go = result.Asset!.GetMainAsset<GameObject>();

            // collect all renderers
            List<AttachmentRegularAsset.RendererInfo> rendererInfos = AttachmentRegularAsset.RENDERER_INFO_POOL.Get();

            using PoolExtensions.Scope<List<SkinnedMeshRenderer>> pooledList = go.GetComponentsInChildrenIntoPooledList<SkinnedMeshRenderer>();

            foreach (SkinnedMeshRenderer skinnedMeshRenderer in pooledList.Value)
                rendererInfos.Add(new AttachmentRegularAsset.RendererInfo(skinnedMeshRenderer, skinnedMeshRenderer.sharedMaterial));

            return new AttachmentRegularAsset(go, rendererInfos, result.Asset);
        }

        public static AttachmentRegularAsset ToRegularAsset(this StreamableLoadingResult<GLTFData> result)
        {
            Debug.Log($"PRAVS - WearablePolymorphicBehaviour.ToRegularAsset() - assetGOName: {result.Asset?.containerGameObject.name}", result.Asset?.containerGameObject);

            GameObject go = result.Asset!.containerGameObject;

            // collect all renderers
            List<AttachmentRegularAsset.RendererInfo> rendererInfos = AttachmentRegularAsset.RENDERER_INFO_POOL.Get();

            using PoolExtensions.Scope<List<SkinnedMeshRenderer>> pooledList = go.GetComponentsInChildrenIntoPooledList<SkinnedMeshRenderer>();

            foreach (SkinnedMeshRenderer skinnedMeshRenderer in pooledList.Value)
                rendererInfos.Add(new AttachmentRegularAsset.RendererInfo(skinnedMeshRenderer, skinnedMeshRenderer.sharedMaterial));

            return new AttachmentRegularAsset(go, rendererInfos, result.Asset);
        }

        public static void AssignWearableAsset(this IWearable wearable, AttachmentRegularAsset attachmentRegularAsset, BodyShape bodyShape)
        {
            ref WearableAssets results = ref wearable.WearableAssetResults[bodyShape];
            results.Results ??= new StreamableLoadingResult<AttachmentAssetBase>?[1];

            results.Results[MAIN_ASSET_INDEX] = new StreamableLoadingResult<AttachmentAssetBase>(attachmentRegularAsset);
        }

        public static bool HasEssentialAssetsResolved(this IWearable wearable, BodyShape bodyShape)
        {
            StreamableLoadingResult<AttachmentAssetBase>?[] results = wearable.WearableAssetResults[bodyShape].Results ?? Array.Empty<StreamableLoadingResult<AttachmentAssetBase>?>();

            if (wearable.Type == WearableType.FacialFeature)
            {
                if (results.Length <= 0) return false;

                // Exclude texture mask from required assets
                StreamableLoadingResult<AttachmentAssetBase>? mainFileAsset = results[0];
                return mainFileAsset is { Succeeded: true };
            }

            for (var i = 0; i < results.Length; i++)
            {
                StreamableLoadingResult<AttachmentAssetBase>? r = results[i];

                if (r is not { IsInitialized: true })
                    return false;
            }

            return true;
        }
    }
}
