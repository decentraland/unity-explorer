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
        private GltFastDownloadProvider gltfDownloadProvider = new GltFastDownloadProvider();
        private GltfastEditorConsoleLogger gltfConsoleLogger = new GltfastEditorConsoleLogger();

        internal LoadGLTFSystem(
            World world,
            IStreamableCache<GLTFData, GetGLTFIntention> cache,
            ISceneData sceneData) : base(world, cache)
        {
            this.sceneData = sceneData;
        }

        protected override async UniTask<StreamableLoadingResult<GLTFData>> FlowInternalAsync(GetGLTFIntention intention, IAcquiredBudget acquiredBudget, IPartitionComponent partition, CancellationToken ct)
        {
            if (!sceneData.SceneContent.TryGetContentUrl(intention.Name!, out var finalDownloadUrl))
                return new StreamableLoadingResult<GLTFData>(new Exception("The content to download couldn't be found"));
            Debug.Log($"content final download URL: {finalDownloadUrl}");

            var gltfImport = new GltfImport(downloadProvider: gltfDownloadProvider, logger: gltfConsoleLogger);

            var gltFastSettings = new ImportSettings
            {
                GenerateMipMaps = false,
                AnisotropicFilterLevel = 3,
                NodeNameMethod = NameImportMethod.OriginalUnique
            };

            bool success = await gltfImport.Load(finalDownloadUrl, gltFastSettings, ct);
            Debug.Log($"LoadGLTFSystem.FlowInternalAsync() - SUCCESS: {success}");

            // Release budget now to not hold it until dependencies are resolved to prevent a deadlock
            acquiredBudget.Release();

            if (success)
                return new StreamableLoadingResult<GLTFData>(new GLTFData(gltfImport));

            return new StreamableLoadingResult<GLTFData>(new Exception("The content to download couldn't be found"));
            // Debug.Log($"LoadGLTFSystem.FlowInternalAsync() - LOADING ERROR: {gltfImport.LoadingError}");
        }
    }
}
