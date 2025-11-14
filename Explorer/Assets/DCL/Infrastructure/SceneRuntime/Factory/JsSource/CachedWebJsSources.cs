using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Optimization.Hashing;
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

        public async UniTask<DownloadedOrCachedData> SceneSourceCodeAsync(URLAddress path,
            CancellationToken ct)
        {
            if (path.Value.StartsWith("file://", StringComparison.Ordinal))
                return await origin.SceneSourceCodeAsync(path, ct);

            using HashKey key = HashKey.FromString(path.Value);
            var getResult = await diskCache.ContentAsync(key, EXTENSION, ct);

            if (getResult is { Success: true, Value: not null }

                // Ignore old UTF-16 encoded cached files.
                && getResult.Value.Value.Memory.Span[1] != 0)
                return new DownloadedOrCachedData(getResult.Value.Value);
            else
            {
                DownloadedOrCachedData sourceCode = await origin.SceneSourceCodeAsync(path, ct);

                var putResult = await diskCache.PutAsync(key, EXTENSION, sourceCode.GetMemoryIterator(),
                    ct);

                if (!putResult.Success)
                    ReportHub.LogWarning(ReportCategory.SCENE_LOADING,
                        $"Could not write to the disk cache because {putResult.Error}");

                return sourceCode;
            }
        }
    }
}
