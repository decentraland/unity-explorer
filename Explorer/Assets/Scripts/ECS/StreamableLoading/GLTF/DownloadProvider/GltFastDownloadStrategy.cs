using Arch.Core;
using DCL.Optimization.PerformanceBudgeting;
using DCL.WebRequests;
using DCL.Diagnostics;
using ECS.Prioritization.Components;
using SceneRunner.Scene;

namespace ECS.StreamableLoading.GLTF.DownloadProvider
{
    public interface IGltFastDownloadStrategy
    {
        IGLTFastDisposableDownloadProvider CreateDownloadProvider(World world, GetGLTFIntention intention, IPartitionComponent partitionComponent, ReportData reportData, IWebRequestController webRequestController, IAcquiredBudget acquiredBudget);
    }

    public struct GltFastSceneDownloadStrategy : IGltFastDownloadStrategy
    {
        private readonly ISceneData sceneData;

        public GltFastSceneDownloadStrategy(ISceneData sceneData)
        {
            this.sceneData = sceneData;
        }

        public IGLTFastDisposableDownloadProvider CreateDownloadProvider(World world, GetGLTFIntention intention, IPartitionComponent partitionComponent, ReportData reportData, IWebRequestController webRequestController, IAcquiredBudget acquiredBudget) =>
            new GltFastSceneDownloadProvider(world, sceneData, partitionComponent, intention.Name!, reportData, webRequestController, acquiredBudget);
    }

    public struct GltFastGlobalDownloadStrategy : IGltFastDownloadStrategy
    {
        private readonly string contentDownloadUrl;

        public GltFastGlobalDownloadStrategy(string contentDownloadUrl)
        {
            this.contentDownloadUrl = contentDownloadUrl;
        }

        public IGLTFastDisposableDownloadProvider CreateDownloadProvider(World world, GetGLTFIntention intention, IPartitionComponent partitionComponent, ReportData reportData, IWebRequestController webRequestController, IAcquiredBudget acquiredBudget) =>
            new GltFastGlobalDownloadProvider(world, contentDownloadUrl, partitionComponent, reportData, webRequestController, acquiredBudget);
    }
}
