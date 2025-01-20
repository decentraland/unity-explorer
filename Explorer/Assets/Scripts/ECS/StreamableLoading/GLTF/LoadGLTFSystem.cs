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
            if ((sceneData != null && !sceneData.SceneContent.TryGetContentUrl(intention.Name!, out _)) || !contentSourceUrlAvailable)
                return new StreamableLoadingResult<GLTFData>(
                    reportData,
                    new Exception("The content to download couldn't be found"));

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

            bool success = await gltfImport.Load(intention.Name, gltFastSettings, ct);
            gltfSceneDownloadProvider?.Dispose();
            gltfGlobalDownloadProvider?.Dispose();

            if (success)
            {
                // We do the GameObject instantiation in this system since 'InstantiateMainSceneAsync()' is async.
                var rootContainer = new GameObject(gltfImport.GetSceneName(0));

                // Let the upper layer decide what to do with the root
                rootContainer.SetActive(false);

                await InstantiateGltfAsync(gltfImport, rootContainer.transform);

                return new StreamableLoadingResult<GLTFData>(new GLTFData(gltfImport, rootContainer));
            }

            return new StreamableLoadingResult<GLTFData>(
                reportData,
                new Exception("The content to download couldn't be found"));
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
    }
}
