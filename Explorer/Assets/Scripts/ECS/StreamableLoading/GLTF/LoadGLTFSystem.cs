using Arch.Core;
using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.GLTFast.Wrappers;
using DCL.Optimization.PerformanceBudgeting;
using DCL.WebRequests;
using ECS.Prioritization.Components;
using ECS.StreamableLoading.Cache;
using ECS.StreamableLoading.Common.Components;
using ECS.StreamableLoading.Common.Systems;
using GLTFast;
using GLTFast.Materials;
using SceneRunner.Scene;
using System;
using System.Threading;
using UnityEngine;

namespace ECS.StreamableLoading.GLTF
{
    [UpdateInGroup(typeof(StreamableLoadingGroup))]
    public partial class LoadGLTFSystem: LoadSystemBase<GLTFData, GetGLTFIntention>
    {
        private static MaterialGenerator gltfMaterialGenerator = new DecentralandMaterialGenerator("DCL/Scene");

        private readonly ISceneData? sceneData;
        private readonly string? contentSourceUrl;
        private readonly IWebRequestController webRequestController;
        private readonly GltFastReportHubLogger gltfConsoleLogger = new GltFastReportHubLogger();

        internal LoadGLTFSystem(World world, IStreamableCache<GLTFData, GetGLTFIntention> cache, IWebRequestController webRequestController, ISceneData? sceneData = null, string? contentSourceUrl = null) : base(world, cache)
        {
            this.sceneData = sceneData;
            this.contentSourceUrl = contentSourceUrl;
            this.webRequestController = webRequestController;
        }

        protected override async UniTask<StreamableLoadingResult<GLTFData>> FlowInternalAsync(GetGLTFIntention intention, IAcquiredBudget acquiredBudget, IPartitionComponent partition, CancellationToken ct)
        {
            var reportData = new ReportData(GetReportCategory());
            bool contentSourceUrlAvailable = !string.IsNullOrEmpty(contentSourceUrl);

            if (!contentSourceUrlAvailable && (sceneData == null || !sceneData.SceneContent.TryGetContentUrl(intention.Name!, out _)))
                return new StreamableLoadingResult<GLTFData>(reportData, new Exception("The content to download couldn't be found"));

            // Acquired budget is released inside GLTFastDownloadedProvider once the GLTF has been fetched
            GltFastSceneDownloadProvider? gltfSceneDownloadProvider = sceneData != null ? new GltFastSceneDownloadProvider(World, sceneData, partition, intention.Name!, reportData, webRequestController, acquiredBudget) : null;
            GltFastGlobalDownloadProvider? gltfGlobalDownloadProvider = contentSourceUrlAvailable ? new GltFastGlobalDownloadProvider(World, contentSourceUrl!, partition, reportData, webRequestController, acquiredBudget) : null;

            var gltfImport = new GltfImport(
                downloadProvider: contentSourceUrlAvailable ? gltfGlobalDownloadProvider : gltfSceneDownloadProvider,
                logger: gltfConsoleLogger,
                materialGenerator: gltfMaterialGenerator);

            var gltFastSettings = new ImportSettings
            {
                NodeNameMethod = NameImportMethod.OriginalUnique,
                AnisotropicFilterLevel = 0,
                GenerateMipMaps = false,
            };

            // bool success = await gltfImport.Load(intention.Name, gltFastSettings, ct);
            bool success = await gltfImport.Load(contentSourceUrlAvailable ? intention.Hash : intention.Name, gltFastSettings, ct);
            gltfSceneDownloadProvider?.Dispose();
            gltfGlobalDownloadProvider?.Dispose();

            if (success)
            {
                // We do the GameObject instantiation in this system since 'InstantiateMainSceneAsync()' is async.
                var rootContainer = new GameObject(gltfImport.GetSceneName(0));

                // Let the upper layer decide what to do with the root
                rootContainer.SetActive(false);

                await InstantiateGltfAsync(gltfImport, rootContainer.transform);

                // TODO: FOR WEARABLES THE TEXTURE HAS TO BE COMPRESSED IN A SPECIFIC TYPE:
                // https://github.com/decentraland/asset-bundle-converter/blob/741e4e380d7ac83e58650b91b9c76157126f2393/asset-bundle-converter/Assets/AssetBundleConverter/AssetBundleConverter.cs#L815~L843
                if (contentSourceUrlAvailable)
                    PatchTexturesForWearable(gltfImport);

                return new StreamableLoadingResult<GLTFData>(new GLTFData(gltfImport, rootContainer));
            }

            return new StreamableLoadingResult<GLTFData>(
                reportData,
                new Exception("The content to download couldn't be found"));
        }

        private void PatchTexturesForWearable(GltfImport gltfImport)
        {
            for (int i = 0; i < gltfImport.TextureCount; i++)
            {
                var originalTexture = gltfImport.GetTexture(i);

                // Ensure the tex ends up being RGBA32 for all wearable textures that come from raw GLTFs
                // Note: BC7 (asset bundle textures optimization) cannot be compressed in runtime with
                // Unity so a different format was chosen: RGBA32
                var compressedTexture = EnsureRGBA32Format(originalTexture);
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
                        var go = new GameObject($"{rootContainerTransform.name}_{i}");
                        Transform goTransform = go.transform;
                        goTransform.SetParent(rootContainerTransform, false);
                        targetTransform = goTransform;
                    }

                    await gltfImport.InstantiateSceneAsync(targetTransform, i);
                }
            else
                await gltfImport.InstantiateSceneAsync(rootContainerTransform);
        }

        private static Texture2D EnsureRGBA32Format(Texture2D sourceTexture)
        {
            if (sourceTexture.format == TextureFormat.RGBA32)
                return sourceTexture;

            Debug.Log($"PRAVS - {sourceTexture.format} -> RGBA32");

            // Most likely the source texture won't be flagged as
            // readable so the RenderTexture approach has to be used
            RenderTexture rt = RenderTexture.GetTemporary(
                sourceTexture.width,
                sourceTexture.height,
                0,
                RenderTextureFormat.ARGB32);

            try
            {
                Graphics.Blit(sourceTexture, rt);

                // Borrow active RT
                RenderTexture previous = RenderTexture.active;
                RenderTexture.active = rt;

                Texture2D rgba32Texture = new Texture2D(
                    sourceTexture.width,
                    sourceTexture.height,
                    TextureFormat.RGBA32,
                    false,
                    false);

                rgba32Texture.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
                rgba32Texture.Apply();

                // Return previously active RT
                RenderTexture.active = previous;

                return rgba32Texture;
            }
            finally
            {
                RenderTexture.ReleaseTemporary(rt);
            }
        }
    }
}
