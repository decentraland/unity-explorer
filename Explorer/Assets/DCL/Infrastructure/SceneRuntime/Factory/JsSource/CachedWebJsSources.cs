using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
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

        public async UniTask<DataHolder> SceneSourceCodeAsync(URLAddress path, CancellationToken ct)
        {
            string key = path.Value;

            if (key.StartsWith("file://", StringComparison.Ordinal))
                return await origin.SceneSourceCodeAsync(path, ct);

            var result = await diskCache.ContentAsync(HashKey.FromString(key), EXTENSION, ct);

            if (!result.Success || result.Value == null)
                throw new Exception($"CachedWebJsSources: SceneSourceCodeAsync failed for url {path.Value}: {result.Error!.Value.State} {result.Error!.Value.Message}");

            return new DataHolder(result.Value.Value);
        }
    }
}
