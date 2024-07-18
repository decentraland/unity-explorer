using Arch.Core;
using Arch.SystemGroups;
using Arch.SystemGroups.Metadata;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Optimization.PerformanceBudgeting;
using ECS.Prioritization.Components;
using ECS.StreamableLoading.AssetBundles;
using ECS.StreamableLoading.Cache;
using ECS.StreamableLoading.Common.Components;
using ECS.StreamableLoading.Common.Systems;
using GLTFast;
using GLTFast.Loading;
using SceneRunner.Scene;
using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace ECS.StreamableLoading.GLTF
{
    [UpdateInGroup(typeof(StreamableLoadingGroup))]
    //[LogCategory(ReportCategory.ASSET_BUNDLES)]
    public partial class LoadGLTFSystem: LoadSystemBase<GLTFData, GetGLTFIntention>
    {
        private ISceneData sceneData;
        private GltFastDownloadProvider gltfDownloadProvider;
        private GltfastEditorConsoleLogger gltfConsoleLogger = new GltfastEditorConsoleLogger(); // TODO: Remove ???

        internal LoadGLTFSystem(
            World world,
            IStreamableCache<GLTFData, GetGLTFIntention> cache,
            ISceneData sceneData,
            IPartitionComponent partitionComponent) : base(world, cache)
        {
            this.sceneData = sceneData;

            // Inject sceneData into GltFastDownloadProvider???
            // sceneData.TryGetMediaUrl()

            gltfDownloadProvider = new GltFastDownloadProvider(World, sceneData, partitionComponent);
        }

        protected override async UniTask<StreamableLoadingResult<GLTFData>> FlowInternalAsync(GetGLTFIntention intention, IAcquiredBudget acquiredBudget, IPartitionComponent partition, CancellationToken ct)
        {
            if (!sceneData.SceneContent.TryGetContentUrl(intention.Name!, out var finalDownloadUrl))
                return new StreamableLoadingResult<GLTFData>(new Exception("The content to download couldn't be found"));
            Debug.Log($"content final download URL: {finalDownloadUrl}");

            gltfDownloadProvider.targetGltfOriginalPath = intention.Name!; // TODO: look for a better way
            var gltfImport = new GltfImport(downloadProvider: gltfDownloadProvider, logger: gltfConsoleLogger);

            var gltFastSettings = new ImportSettings
            {
                GenerateMipMaps = false,
                AnisotropicFilterLevel = 3,
                NodeNameMethod = NameImportMethod.OriginalUnique
            };

            bool success = await gltfImport.Load(finalDownloadUrl, gltFastSettings, ct);
            Debug.Log($"LoadGLTFSystem.FlowInternalAsync() - SUCCESS ({intention.Name}): {success}");

            // Release budget now to not hold it until dependencies are resolved to prevent a deadlock
            acquiredBudget.Release();

            if (success)
            {
                // We do the GameObject instantiation in this system since 'InstantiateMainSceneAsync()' is async.
                var rootContainer = new GameObject(gltfImport.GetSceneName(0));

                // Let the upper layer decide what to do with the root
                rootContainer.SetActive(false);

                await InstantiateGltfAsync(gltfImport, rootContainer.transform);

                return new StreamableLoadingResult<GLTFData>(new GLTFData(gltfImport, rootContainer));
            }

            return new StreamableLoadingResult<GLTFData>(new Exception("The content to download couldn't be found"));
            // Debug.Log($"LoadGLTFSystem.FlowInternalAsync() - LOADING ERROR: {gltfImport.LoadingError}");
        }

        public async UniTask InstantiateGltfAsync(GltfImport gltfImport, Transform rootContainerTransform)
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
