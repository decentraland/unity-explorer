// GLTF is not supported at the moment
#if !UNITY_WEBGL

using Arch.Core;
using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.GLTFast.Wrappers;
using DCL.WebRequests;
using ECS.Prioritization.Components;
using ECS.StreamableLoading.Cache;
using ECS.StreamableLoading.Common.Components;
using ECS.StreamableLoading.Common.Systems;
using ECS.StreamableLoading.GLTF.DownloadProvider;
using GLTFast;
using GLTFast.Materials;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using Utility;

namespace ECS.StreamableLoading.GLTF
{
    [UpdateInGroup(typeof(StreamableLoadingGroup))]
    public partial class LoadGLTFSystem: LoadSystemBase<GLTFData, GetGLTFIntention>
    {
        private static MaterialGenerator gltfMaterialGenerator = new DecentralandMaterialGenerator("DCL/Scene");

        // Parented under the injected pools root so Unity tears it down with the rest of the pool
        // infrastructure at app exit; not disposed explicitly.
        private static Transform? rootContainerParent;

        private readonly IWebRequestController webRequestController;
        private readonly GltFastReportHubLogger gltfConsoleLogger = new GltFastReportHubLogger();
        private readonly bool patchTexturesFormat;
        private readonly bool importFilesByHash;
        private readonly bool isLocalSceneDevelopment;
        private readonly IGltFastDownloadStrategy downloadStrategy;

        internal LoadGLTFSystem(World world,
            IStreamableCache<GLTFData, GetGLTFIntention> cache,
            IWebRequestController webRequestController,
            bool patchTexturesFormat,
            bool importFilesByHash,
            bool isLocalSceneDevelopment,
            IGltFastDownloadStrategy downloadStrategy,
            Transform poolsRoot) : base(world, cache)
        {
            this.webRequestController = webRequestController;
            this.patchTexturesFormat = patchTexturesFormat;
            this.importFilesByHash = importFilesByHash;
            this.isLocalSceneDevelopment = isLocalSceneDevelopment;
            this.downloadStrategy = downloadStrategy;

            if (rootContainerParent == null)
            {
                var go = new GameObject($"POOL_{nameof(GLTFData)}_TEMPLATES");
                go.SetActive(false);
                go.transform.SetParent(poolsRoot, worldPositionStays: false);
                rootContainerParent = go.transform;
            }
        }

        protected override async UniTask<StreamableLoadingResult<GLTFData>> FlowInternalAsync(GetGLTFIntention intention, StreamableLoadingState state, IPartitionComponent partition, CancellationToken ct)
        {
            var reportData = new ReportData(GetReportCategory());

            // Acquired budget is released inside GLTFastDownloadedProvider once the GLTF has been fetched
            // Cannot inject DownloadProvider from outside, because it needs the AcquiredBudget and PartitionComponent
            using IGLTFastDisposableDownloadProvider gltFastDownloadProvider = downloadStrategy.CreateDownloadProvider(World, intention, partition, reportData, webRequestController, state.AcquiredBudget!);
            gltFastDownloadProvider.SetContentMappings(intention.ContentMappings);

            GltfImport? gltfImport = null;
            GameObject? rootContainer = null;

            try
            {
                gltfImport = new GltfImport(
                    downloadProvider: gltFastDownloadProvider,
                    logger: gltfConsoleLogger,
                    materialGenerator: gltfMaterialGenerator);

                var gltFastSettings = new ImportSettings
                {
                    NodeNameMethod = NameImportMethod.OriginalUnique,
                    AnisotropicFilterLevel = 0,
                    GenerateMipMaps = false,
                    AnimationMethod = intention.MecanimAnimationClips ? AnimationMethod.Mecanim : AnimationMethod.Legacy,
                };

                bool success = await gltfImport.Load(importFilesByHash ? intention.Hash : intention.Name, gltFastSettings, ct);

                if (!success)
                {
                    gltfImport.Dispose();
                    gltfImport = null;
                    return new StreamableLoadingResult<GLTFData>(reportData, new Exception("The content to download couldn't be found"));
                }

                // We do the GameObject instantiation in this system since 'InstantiateMainSceneAsync()' is async.
                rootContainer = new GameObject(gltfImport.GetSceneName(0));

                // Let the upper layer decide what to do with the root
                rootContainer.SetActive(false);

                rootContainer.transform.SetParent(rootContainerParent, worldPositionStays: false);

                await InstantiateGltfAsync(gltfImport, rootContainer.transform);

                // Wearable path needs source textures readable so PatchTexturesForWearable can build
                // RGBA32 copies; the gltf-container path doesn't, so drop the CPU mirror to halve RAM.
                if (patchTexturesFormat)
                    PatchTexturesForWearable(gltfImport);
                else
                    DropTexturesCpuMirror(gltfImport);

                // Capture hierarchy paths for local scene development debugging
                var hierarchyPaths = isLocalSceneDevelopment ? CaptureHierarchyPaths(rootContainer) : null;

                // Ownership of gltfImport and rootContainer transfers to GLTFData — null out locals so the catch
                // block does not double-dispose. Per-consumer ref counting: LoadSystemBase.ApplyLoadedResult
                // calls cache.AddReference, and each consumer's GltfContainerAsset.Dispose dereferences.
                var gltfData = new GLTFData(gltfImport, rootContainer, hierarchyPaths);
                gltfImport = null;
                rootContainer = null;
                return new StreamableLoadingResult<GLTFData>(gltfData);
            }
            catch
            {
                if (rootContainer != null)
                    UnityObjectUtils.SafeDestroy(rootContainer);

                gltfImport?.Dispose();
                throw;
            }
        }

        /// <summary>
        /// LoadSystemBase invokes this when a successful result is abandoned (e.g. cancellation after Load completes
        /// but before any consumer's ApplyLoadedResult added a reference). If any consumer has already claimed it the
        /// ref count is > 0 and we leave teardown to them.
        /// </summary>
        protected override void DisposeAbandonedResult(GLTFData asset)
        {
            // The guard expresses intent (skip if a consumer claimed the asset); Dispose() repeats
            // the same CanBeDisposed check internally, so do not pass force: true here.
            if (asset.CanBeDisposed())
                asset.Dispose();
        }

        /// <summary>
        ///     Drops the CPU-side mirror of GLTFast's loaded textures. Safe post-Load because
        ///     sampler variants are already cloned and nothing in the gltf-container path samples
        ///     pixels afterwards (materials bind to GPU; EnsureRGBA32Format uses Graphics.Blit).
        /// </summary>
        private static void DropTexturesCpuMirror(GltfImport gltfImport)
        {
            for (int i = 0; i < gltfImport.TextureCount; i++)
            {
                Texture2D? tex = gltfImport.GetTexture(i);
                if (tex != null && tex.isReadable)
                    tex.Apply(updateMipmaps: false, makeNoLongerReadable: true);
            }
        }

        private void PatchTexturesForWearable(GltfImport gltfImport)
        {
            for (int i = 0; i < gltfImport.TextureCount; i++)
            {
                var originalTexture = gltfImport.GetTexture(i);

                // Note: BC7 (asset bundle textures optimization) cannot be compressed in runtime with
                // Unity so a different format was chosen: RGBA32
                var compressedTexture = TextureUtilities.EnsureRGBA32Format(originalTexture);
                if (compressedTexture == originalTexture)
                    continue;

                // Copy properties from original texture
                compressedTexture.wrapMode = originalTexture.wrapMode;
                compressedTexture.filterMode = originalTexture.filterMode;
                compressedTexture.anisoLevel = originalTexture.anisoLevel;

                // Replace texture in all materials that use it
                for (int matIndex = 0; matIndex < gltfImport.MaterialCount; matIndex++)
                {
                    var material = gltfImport.GetMaterial(matIndex);

                    // Check all texture properties in the material
                    foreach (string propertyName in material.GetTexturePropertyNames())
                    {
                        if (material.GetTexture(propertyName) == originalTexture)
                        {
                            material.SetTexture(propertyName, compressedTexture);
                        }
                    }
                }

                // Clean up original texture
                UnityEngine.Object.Destroy(originalTexture);
            }
        }

        private async UniTask InstantiateGltfAsync(GltfImport gltfImport, Transform rootContainerTransform)
        {
            if (gltfImport.SceneCount > 1)
                for (int i = 0; i < gltfImport.SceneCount; i++)
                {
                    var targetTransform = rootContainerTransform;

                    if (i != 0)
                    {
                        var go = new GameObject($"{rootContainerTransform.name}_{i.ToString()}");
                        Transform goTransform = go.transform;
                        goTransform.SetParent(rootContainerTransform, false);
                        targetTransform = goTransform;
                    }

                    await gltfImport.InstantiateSceneAsync(targetTransform, i);
                }
            else
                await gltfImport.InstantiateSceneAsync(rootContainerTransform);
        }

        /// <summary>
        /// Captures all possible paths in the GLTF GameObject hierarchy for LSD debugging purposes
        /// </summary>
        private static List<string> CaptureHierarchyPaths(GameObject rootContainer)
        {
            var paths = new List<string>();

            // Start from the GLTF root's children
            if (rootContainer.transform.childCount > 0)
            {
                var gltfRoot = rootContainer.transform.GetChild(0);
                for (int i = 0; i < gltfRoot.childCount; i++)
                {
                    CaptureHierarchyPathsRecursive(gltfRoot.GetChild(i), "", paths);
                }
            }

            // Sort paths for easier reading in debug output
            paths.Sort();
            return paths;
        }

        /// <summary>
        /// Recursively captures all paths in the hierarchy
        /// </summary>
        private static void CaptureHierarchyPathsRecursive(Transform transform, string currentPath, List<string> paths)
        {
            // Build the path for this transform
            string transformPath = string.IsNullOrEmpty(currentPath)
                ? transform.name
                : $"{currentPath}/{transform.name}";

            // Add this path if it has a Renderer
            if (transform.GetComponent<Renderer>() != null)
                paths.Add(transformPath);

            // Recursively process children
            for (int i = 0; i < transform.childCount; i++)
            {
                CaptureHierarchyPathsRecursive(transform.GetChild(i), transformPath, paths);
            }
        }
    }
}

#endif
