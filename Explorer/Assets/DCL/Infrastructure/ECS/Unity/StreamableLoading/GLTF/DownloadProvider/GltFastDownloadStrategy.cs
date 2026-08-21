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
        IGLTFastDisposableDownloadProvider CreateDownloadProvider(World world, GetGLTFIntention intention, IPartitionComponent partitionComponent, ReportData reportData, IWebRequestController webRequestController, IAcquiredBudget? acquiredBudget);

        /// <summary>
        ///     True while every external file the import fetched still resolves to the URL it was
        ///     imported from, i.e. the cached data can be reused as-is.
        /// </summary>
        bool AreExternalDependenciesUpToDate(GLTFData data);
    }

    public struct GltFastSceneDownloadStrategy : IGltFastDownloadStrategy
    {
        private readonly ISceneData sceneData;

        public GltFastSceneDownloadStrategy(ISceneData sceneData)
        {
            this.sceneData = sceneData;
        }

        public IGLTFastDisposableDownloadProvider CreateDownloadProvider(World world, GetGLTFIntention intention, IPartitionComponent partitionComponent, ReportData reportData, IWebRequestController webRequestController, IAcquiredBudget? acquiredBudget) =>
            new GltFastSceneDownloadProvider(world, sceneData, partitionComponent, intention.Name!, reportData, webRequestController, acquiredBudget);

        public bool AreExternalDependenciesUpToDate(GLTFData data) =>
            GltfExternalDependency.AreUpToDate(data.ExternalDependencies, sceneData.SceneContent);
    }

    public struct GltFastGlobalDownloadStrategy : IGltFastDownloadStrategy
    {
        private readonly string contentDownloadUrl;

        public GltFastGlobalDownloadStrategy(string contentDownloadUrl)
        {
            this.contentDownloadUrl = contentDownloadUrl;
        }

        public IGLTFastDisposableDownloadProvider CreateDownloadProvider(World world, GetGLTFIntention intention, IPartitionComponent partitionComponent, ReportData reportData, IWebRequestController webRequestController, IAcquiredBudget? acquiredBudget) =>
            new GltFastGlobalDownloadProvider(world, contentDownloadUrl, partitionComponent, reportData, webRequestController, acquiredBudget);

        // Global content is content-addressed and immutable: a dependency cannot change while the
        // GLTF's own hash stays the same.
        public bool AreExternalDependenciesUpToDate(GLTFData data) =>
            true;
    }

    public struct GltFastRealmDataDownloadStrategy : IGltFastDownloadStrategy
    {
        private readonly IRealmData realmData;

        public GltFastRealmDataDownloadStrategy(IRealmData realmData)
        {
            this.realmData = realmData;
        }

        public IGLTFastDisposableDownloadProvider CreateDownloadProvider(World world, GetGLTFIntention intention, IPartitionComponent partitionComponent, ReportData reportData, IWebRequestController webRequestController, IAcquiredBudget? acquiredBudget) =>
            new GltFastGlobalDownloadProvider(world, realmData.Ipfs.ContentBaseUrl.Value, partitionComponent, reportData, webRequestController, acquiredBudget);

        // Realm content is content-addressed and immutable: a dependency cannot change while the
        // GLTF's own hash stays the same.
        public bool AreExternalDependenciesUpToDate(GLTFData data) =>
            true;
    }
}
