using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Optimization;
using ECS.StreamableLoading.Cache.Disk;
using SceneRuntime.Factory.WebSceneSource;
using System.Threading;

namespace SceneRuntime.Factory.JsSource
{
    public class CachedWebJsSources : IWebJsSources
    {
        private readonly IWebJsSources origin;
        private readonly IJsSourcesCache cache;
        private readonly IDiskCache<string> diskCache;
        private const string EXTENSION = "js";

        public CachedWebJsSources(IWebJsSources origin, IJsSourcesCache cache, IDiskCache<string> diskCache)
        {
            this.origin = origin;
            this.cache = cache;
            this.diskCache = diskCache;
        }

        public async UniTask<string> SceneSourceCodeAsync(URLAddress path, CancellationToken ct)
        {
            if (cache.TryGet(path, out string? result))
                return result!;

            var diskResult = await diskCache.ContentAsync(path.Value, EXTENSION, ct);

            if (diskResult.Success)
            {
                var option = diskResult.Value;

                if (option.Has)
                {
                    cache.Cache(path, option.Value);
                    return option.Value;
                }
            }
            else
                ReportHub.LogError(
                    ReportCategory.SCENE_LOADING,
                    $"Error getting js disk cache content for '{path}' - {diskResult.Error!.Value.State} {diskResult.Error!.Value.Message}"
                );

            string sourceCode = await origin.SceneSourceCodeAsync(path, ct);
            cache.Cache(path, sourceCode);
            diskCache.PutAsync(path.Value, EXTENSION, sourceCode, ct).Forget();

            return sourceCode;
        }
    }
}
