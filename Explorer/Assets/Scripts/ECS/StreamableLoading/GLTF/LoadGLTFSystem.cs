using Arch.Core;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.GLTFast.Wrappers;
using DCL.Optimization.PerformanceBudgeting;
using ECS.Prioritization.Components;
using ECS.StreamableLoading.Cache;
using ECS.StreamableLoading.Common.Components;
using ECS.StreamableLoading.Common.Systems;
using GLTFast;
using GLTFast.Materials;
using System;
using System.Threading;
using UnityEngine;

namespace ECS.StreamableLoading.GLTF
{
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial class LoadGLTFSystem: LoadSystemBase<GLTFData, GetGLTFIntention>
    {
        private static MaterialGenerator gltfMaterialGenerator = new DecentralandMaterialGenerator("DCL/Scene");
        private GltFastReportHubLogger gltfConsoleLogger = new GltFastReportHubLogger();

        internal LoadGLTFSystem(World world, IStreamableCache<GLTFData, GetGLTFIntention> cache) : base(world, cache)
        {
        }

        protected override async UniTask<StreamableLoadingResult<GLTFData>> FlowInternalAsync(GetGLTFIntention intention, IAcquiredBudget acquiredBudget, IPartitionComponent partition, CancellationToken ct)
        {
            if (!intention.SceneData.SceneContent.TryGetContentUrl(intention.Name!, out _))
                return new StreamableLoadingResult<GLTFData>(
                    new ReportData(GetReportCategory()),
                    new Exception("The content to download couldn't be found"));

            // Acquired budget is released inside GLTFastDownloadedProvider once the GLTF has been fetched
            GltFastDownloadProvider gltfDownloadProvider = new GltFastDownloadProvider(World, intention.SceneData, partition, intention.Name!, acquiredBudget);
            var gltfImport = new GltfImport(
                downloadProvider: gltfDownloadProvider,
                logger: gltfConsoleLogger,
                materialGenerator: gltfMaterialGenerator);

            AnimationMethod animationMethod = intention.MecanimAnimationClips ? AnimationMethod.Mecanim : AnimationMethod.Legacy;

            var gltFastSettings = new ImportSettings
            {
                NodeNameMethod = NameImportMethod.OriginalUnique,
                AnisotropicFilterLevel = 0,
                GenerateMipMaps = false,
                AnimationMethod = animationMethod,
            };

            bool success = await gltfImport.Load(intention.Name, gltFastSettings, ct);
            gltfDownloadProvider.Dispose();

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
                new ReportData(GetReportCategory()),
                new Exception("The content to download couldn't be found"));
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
