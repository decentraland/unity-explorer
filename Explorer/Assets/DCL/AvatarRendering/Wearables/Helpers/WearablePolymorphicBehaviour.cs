using Arch.Core;
using AssetManagement;
using CommunicationData.URLHelpers;
using DCL.AvatarRendering.Loading.Assets;
using DCL.AvatarRendering.Loading.Components;
using DCL.AvatarRendering.Loading.DTO;
using DCL.AvatarRendering.Wearables.Components;
using DCL.AvatarRendering.Wearables.Components.Intentions;
using DCL.Diagnostics;
using DCL.Ipfs;
using DCL.Optimization.Pools;
using DCL.Utility;
using ECS.Prioritization.Components;
using ECS.StreamableLoading.AssetBundles;
using ECS.StreamableLoading.Common.Components;
using ECS.StreamableLoading.GLTF;
using ECS.StreamableLoading.Textures;
using System;
using System.Collections.Generic;
using UnityEngine;
using Utility;
using AssetBundlePromise = ECS.StreamableLoading.Common.AssetPromise<ECS.StreamableLoading.AssetBundles.AssetBundleData, ECS.StreamableLoading.AssetBundles.GetAssetBundleIntention>;
using RawGltfPromise = ECS.StreamableLoading.Common.AssetPromise<ECS.StreamableLoading.GLTF.GLTFData, ECS.StreamableLoading.GLTF.GetGLTFIntention>;
using TexturePromise = ECS.StreamableLoading.Common.AssetPromise<ECS.StreamableLoading.Textures.TextureData, ECS.StreamableLoading.Textures.GetTextureIntention>;
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

        // Wearables that ship a facial-expression atlas use these suffixes so the loader picks
        // the matching main + mask pair instead of the legacy single-frame face texture.
        // Example pair: `eyes_12_expressions.png` + `eyes_12_expressions_mask.png`.
        private const string EXPRESSIONS_MAIN_FILE_SUFFIX = "_expressions.png";
        private const string EXPRESSIONS_MASK_FILE_SUFFIX = "_expressions_mask.png";
        private const string LEGACY_MASK_FILE_SUFFIX = "_mask.png";

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
            BodyShape bodyShape = intention.BodyShape;

            switch (wearable.Type)
            {
                case WearableType.FacialFeature:
                    return TryCreateFacialFeaturePromises(
                        in intention,
                        customStreamingSubdirectory,
                        wearable,
                        partitionComponent,
                        bodyShape,
                        world,
                        reportData);
                default:
                    return TryCreateSingleGameObjectPromise(
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
            in GetWearablesByPointersIntention intention,
            URLSubdirectory customStreamingSubdirectory,
            IWearable wearable,
            IPartitionComponent partitionComponent,
            BodyShape bodyShape,
            World world,
            ReportData reportData)
        {
            ref WearableAssets wearableAssets = ref InitializeResultsArray(wearable, bodyShape, 1);

            return TryCreateMainFilePromise(typeof(GameObject), intention, customStreamingSubdirectory, wearable, partitionComponent, ref wearableAssets, bodyShape, world, reportData);
        }

        /// <summary>
        ///     Facial feature can consist of the main texture and the mask. When the wearable
        ///     ships an `*_expressions.png` atlas (4×4 expression grid), the loader substitutes it
        ///     for the legacy single-frame texture and pairs the `*_expressions_mask.png` as mask.
        ///     The DTO's mainFile pointer is unchanged; the substitution is loader-side only and
        ///     flags <see cref="IWearable.HasFacialExpressionsTexture"/> so renderers can atlas-slice.
        /// </summary>
        private static bool TryCreateFacialFeaturePromises(
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
            return TryCreateFacialFeatureMainPromise(in intention, customStreamingSubdirectory, wearable, partitionComponent, ref wearableAssets, bodyShape, world, reportData)
                   | TryCreateMaskPromise(intention, customStreamingSubdirectory, wearable, partitionComponent, ref wearableAssets, bodyShape, world);
        }

        private static bool TryCreateFacialFeatureMainPromise(
            in GetWearablesByPointersIntention intention,
            URLSubdirectory customStreamingSubdirectory,
            IWearable wearable,
            IPartitionComponent partitionComponent,
            ref WearableAssets wearableAssets,
            BodyShape bodyShape,
            World world,
            ReportData reportData)
        {
            if (wearableAssets.Results[MAIN_ASSET_INDEX] != null)
                return false;

            bool hasExpressionsTexture = wearable.TryGetFileHashConditional(bodyShape,
                static content => content.EndsWith(EXPRESSIONS_MAIN_FILE_SUFFIX, StringComparison.OrdinalIgnoreCase),
                out string? mainHash);

            // Fallback path: no `*_expressions.png` content entry, OR the wearable's declared
            // mainFile itself is one (TryGetFileHashConditional skips the mainFile).
            if (!hasExpressionsTexture)
            {
                if (!wearable.TryGetMainFileHash(bodyShape, out mainHash))
                {
                    wearableAssets.Results[MAIN_ASSET_INDEX] =
                        new StreamableLoadingResult<AttachmentAssetBase>(reportData, new Exception("Main file hash not found"));
                    return false;
                }

                hasExpressionsTexture = TryGetMainFileKey(wearable, bodyShape, out string? mainFileKey)
                                        && mainFileKey != null
                                        && mainFileKey.EndsWith(EXPRESSIONS_MAIN_FILE_SUFFIX, StringComparison.OrdinalIgnoreCase);
            }

            wearable.HasFacialExpressionsTexture = hasExpressionsTexture;

            CreatePromise(typeof(Texture), intention, customStreamingSubdirectory, mainHash!,
                wearable, MAIN_ASSET_INDEX, partitionComponent, world);
            return true;
        }

        private static bool TryGetMainFileKey(IAvatarAttachment attachment, BodyShape bodyShape, out string? key)
        {
            AvatarAttachmentDTO? dto = attachment.DTO;

            if (dto?.Metadata.AbstractData.representations != null)
            {
                AvatarAttachmentDTO.Representation[] representations = dto.Metadata.AbstractData.representations;

                for (var i = 0; i < representations.Length; i++)
                {
                    AvatarAttachmentDTO.Representation representation = representations[i];

                    if (representation.bodyShapes.Contains(bodyShape))
                    {
                        key = representation.mainFile;
                        return true;
                    }
                }
            }

            key = null;
            return false;
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

        private static bool TryCreateMaskPromise(
            GetWearablesByPointersIntention intention, URLSubdirectory customStreamingSubdirectory, IWearable wearable,
            IPartitionComponent partitionComponent, ref WearableAssets wearableAssets, BodyShape bodyShape, World world)
        {
            if (wearableAssets.Results[MASK_ASSET_INDEX] != null)
                return false;

            // Expression-capable wearables pair their atlas with `*_expressions_mask.png`. Other
            // wearables fall back to the legacy `*_mask.png` (excluding the expressions variant to
            // avoid grabbing the wrong file when both pairs ship side-by-side).
            Func<string, bool> contentMatch = wearable.HasFacialExpressionsTexture
                ? static content => content.EndsWith(EXPRESSIONS_MASK_FILE_SUFFIX, StringComparison.OrdinalIgnoreCase)
                : static content => content.EndsWith(LEGACY_MASK_FILE_SUFFIX, StringComparison.OrdinalIgnoreCase)
                                    && !content.EndsWith(EXPRESSIONS_MASK_FILE_SUFFIX, StringComparison.OrdinalIgnoreCase);

            if (!wearable.TryGetFileHashConditional(bodyShape, contentMatch, out string mainFileHash))
            {
                // If there is no mask, we don't need to create a promise for it, and it's not an exception
                wearableAssets.Results[MASK_ASSET_INDEX] = new StreamableLoadingResult<AttachmentAssetBase>((AttachmentTextureAsset)null);
                return false;
            }

            CreatePromise(
                typeof(Texture),
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
                foreach (ContentDefinition content in wearable.DTO.content)
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
                        hash + PlatformUtils.GetCurrentPlatform(),
                        expectedObjectType,
                        permittedSources: intention.PermittedSources,
                        customEmbeddedSubDirectory: customStreamingSubdirectory,
                        assetBundleManifestVersion: wearable.DTO.assetBundleManifestVersion,
                        parentEntityID: wearable.DTO.id,
                        cancellationTokenSource: intention.CancellationTokenSource),
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
            ContentDefinition content,
            GetWearablesByPointersIntention intention,
            T wearable,
            int index,
            IPartitionComponent partitionComponent,
            World world) where T: IAvatarAttachment
        {
            // An index is added to the promises to know to which slot of the WearableAssets it belongs to

            if (content.file.EndsWith(".glb")) // Wearables cannot be ".gltf"
            {
                // We pass in the DTO content mappings (file -> hash) to the promise, so that external assets can be resolved
                var getGltf = GetGLTFIntention.Create(content.file, content.hash, false, wearable.DTO.content);
                var promise = RawGltfPromise.Create(world, getGltf, partitionComponent);
                world.Create(promise, wearable, intention.BodyShape, index);
            }
            else if (content.file.EndsWith(".png")) // Facial Feature Wearables documentation specifies PNG format
            {
                var promise = TexturePromise.Create(world,
                    new GetTextureIntention
                    {
                        // If cancellation token source was not provided a new one will be created
                        CommonArguments = new CommonLoadingArguments(URLAddress.FromString(wearable.DTO.ContentDownloadUrl+content.hash), cancellationTokenSource: intention.CancellationTokenSource),
                        ReportSource = nameof(WearablePolymorphicBehaviour),
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
                {
                    if (result.Asset.TryGetAsset(out Texture texture))
                        return new StreamableLoadingResult<AttachmentAssetBase>(new AttachmentTextureAsset(texture, result.Asset));

                    return new StreamableLoadingResult<AttachmentAssetBase>(result.ReportData, new ArgumentException($"Failed to create facial feature asset for {wearable} from the AB"));
                }
                default:
                    if (result.TryToConvertToRegularAsset(out AttachmentRegularAsset regularAsset))
                        return new StreamableLoadingResult<AttachmentAssetBase>(regularAsset);

                    return new StreamableLoadingResult<AttachmentAssetBase>(result.ReportData, new ArgumentException($"Failed to create wearable asset for {wearable} from the AB"));

            }
        }

        // Raw GLTF Wearable
        public static StreamableLoadingResult<AttachmentAssetBase> ToWearableAsset(this StreamableLoadingResult<GLTFData> result)
        {
            if (result.Succeeded && result.TryToConvertToRegularAsset(out AttachmentRegularAsset regularAsset))
                return new StreamableLoadingResult<AttachmentAssetBase>(regularAsset);

            return new StreamableLoadingResult<AttachmentAssetBase>(result.ReportData, result.Exception!);
        }

        // Raw Facial Feature Wearable
        public static StreamableLoadingResult<AttachmentAssetBase> ToWearableAsset(this StreamableLoadingResult<TextureData> result)
        {
            if (!result.Succeeded) return new StreamableLoadingResult<AttachmentAssetBase>(result.ReportData, result.Exception!);

            // Has to be RGBA32 to work with the avatar shader TextureArrays (Unity cannot compress BC7 in runtime)
            return new StreamableLoadingResult<AttachmentAssetBase>(new AttachmentTextureAsset(TextureUtilities.EnsureRGBA32Format(result.Asset!.EnsureTexture2D()), result.Asset));
        }

        public static bool TryToConvertToRegularAsset(this StreamableLoadingResult<AssetBundleData> result, out AttachmentRegularAsset regularAssetResult)
        {
            if (!result.Asset.TryGetAsset(out GameObject go))
            {
                regularAssetResult = null;
                return false;
            }

            // collect all renderers
            List<AttachmentRegularAsset.RendererInfo> rendererInfos = AttachmentRegularAsset.RENDERER_INFO_POOL.Get();

            using PoolExtensions.Scope<List<SkinnedMeshRenderer>> pooledList = go.GetComponentsInChildrenIntoPooledList<SkinnedMeshRenderer>();

            foreach (SkinnedMeshRenderer skinnedMeshRenderer in pooledList.Value)
                rendererInfos.Add(new AttachmentRegularAsset.RendererInfo(skinnedMeshRenderer.sharedMaterial));

            regularAssetResult = new AttachmentRegularAsset(go, rendererInfos, result.Asset);
            return true;
        }

        //GLTFs should never fail
        public static bool TryToConvertToRegularAsset(this StreamableLoadingResult<GLTFData> result, out AttachmentRegularAsset regularAssetResult)
        {
            GameObject go = result.Asset!.Root;

            // collect all renderers
            List<AttachmentRegularAsset.RendererInfo> rendererInfos = AttachmentRegularAsset.RENDERER_INFO_POOL.Get();

            using PoolExtensions.Scope<List<SkinnedMeshRenderer>> pooledList = go.GetComponentsInChildrenIntoPooledList<SkinnedMeshRenderer>();

            foreach (SkinnedMeshRenderer skinnedMeshRenderer in pooledList.Value)
                rendererInfos.Add(new AttachmentRegularAsset.RendererInfo(skinnedMeshRenderer.sharedMaterial));

            regularAssetResult = new AttachmentRegularAsset(go, rendererInfos, result.Asset);
            return true;
        }

        public static bool HasEssentialAssetsResolved(this IWearable wearable, BodyShape bodyShape)
        {
            StreamableLoadingResult<AttachmentAssetBase>?[] results = wearable.WearableAssetResults[bodyShape].Results;

            // We only care for the result in 0
            // If its a regular wearbale, the asset that we need to check the initialization its at 0 (MAIN_ASSET_INDEX)
            // If its a facial feature, we care about the asset at 0, not the mask at 1 (MASK_ASSET_INDEX)
            if (results?[0] is not { IsInitialized: true })
                return false;

            return true;
        }
    }
}
