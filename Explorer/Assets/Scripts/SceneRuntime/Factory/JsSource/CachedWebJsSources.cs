using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Utilities.Extensions;
using ECS.StreamableLoading.Cache.Disk;
using ECS.StreamableLoading.Cache.Generic;
using ECS.StreamableLoading.Cache.InMemory;
using SceneRuntime.Factory.WebSceneSource;
using System;
using System.Threading;

namespace SceneRuntime.Factory.JsSource
{
    public class CachedWebJsSources : IWebJsSources
    {
        private readonly IWebJsSources origin;
        private readonly IGenericCache<string, string> cache;
        private const string EXTENSION = "js";

        public CachedWebJsSources(IWebJsSources origin, IMemoryCache<string, string> cache, IDiskCache<string> diskCache)
        {
            this.origin = origin;
            this.cache = new GenericCache<string, string>(cache, diskCache, static s => s, EXTENSION);
        }

        public async UniTask<string> SceneSourceCodeAsync(URLAddress path, CancellationToken ct)
        {
            string key = path.Value;

            if (key.StartsWith("file://", StringComparison.Ordinal))
                return await origin.SceneSourceCodeAsync(path, ct);

            var result = await cache.ContentOrFetchAsync(key, origin, static v => SceneSourceCodeAsync(v), ct);
            return result.Unwrap().Value.EnsureNotNull();
        }

        private static UniTask<string> SceneSourceCodeAsync((string key, IWebJsSources ctx, CancellationToken token) value) =>
            value.ctx!.SceneSourceCodeAsync(URLAddress.FromString(value.key!), value.token);
    }
}
