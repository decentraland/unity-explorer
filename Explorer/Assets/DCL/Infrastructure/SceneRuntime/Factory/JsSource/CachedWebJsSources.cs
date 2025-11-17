using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Optimization.Hashing;
using DCL.Utility.Types;
using ECS.StreamableLoading.Cache.Disk;
using SceneRuntime.Factory.WebSceneSource;
using System;
using System.Threading;

namespace SceneRuntime.Factory.JsSource
{
    public class CachedWebJsSources : IWebJsSources
    {
        private readonly IWebJsSources origin;
        private readonly IDiskCache diskCache;
        private const string EXTENSION = "js";

        public CachedWebJsSources(IWebJsSources origin, IDiskCache diskCache)
        {
            this.origin = origin;
            this.diskCache = diskCache;
        }

        public async UniTask<Result<DownloadedOrCachedData>> SceneSourceCodeAsync(URLAddress path,
            CancellationToken ct)
        {
            if (path.Value.StartsWith("file://", StringComparison.Ordinal))
                return await origin.SceneSourceCodeAsync(path, ct);

            using HashKey key = HashKey.FromString(path.Value);
            var getResult = await diskCache.ContentAsync(key, EXTENSION, ct);

            if (getResult is { Success: true, Value: not null })
                return Result<DownloadedOrCachedData>.SuccessResult(
                    new DownloadedOrCachedData(getResult.Value.Value));
            else
            {
                Result<DownloadedOrCachedData> sourceCodeResult = await origin.SceneSourceCodeAsync(
                    path, ct);

                if (sourceCodeResult.Success)
                {
                    var putResult = await diskCache.PutAsync(key, EXTENSION,
                        sourceCodeResult.Value.GetMemoryIterator(), ct);

                    if (!putResult.Success)
                        ReportHub.LogWarning(ReportCategory.SCENE_LOADING,
                            $"Could not write to the disk cache because {putResult.Error}");
                }

                return sourceCodeResult;
            }
        }
    }
}
