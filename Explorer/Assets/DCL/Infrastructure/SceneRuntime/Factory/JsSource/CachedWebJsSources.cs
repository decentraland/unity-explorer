using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Utilities.Extensions;
using ECS.StreamableLoading.Cache.Disk;
using ECS.StreamableLoading.Cache.Disk.Cacheables;
using ECS.StreamableLoading.Cache.Generic;
using ECS.StreamableLoading.Cache.InMemory;
using SceneRuntime.Factory.WebSceneSource;
using System;
using System.Threading;
using Utility.Types;

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
            this.cache = new GenericCache<string, string>(cache, diskCache, DiskHashCompute.INSTANCE, EXTENSION);
        }

        public async UniTask<string> SceneSourceCodeAsync(Uri path, CancellationToken ct)
        {
            string key = path.OriginalString;

            if (key.StartsWith("file://", StringComparison.Ordinal))
                return await origin.SceneSourceCodeAsync(path, ct);

            EnumResult<Option<string>, TaskError> result = await cache.ContentOrFetchAsync(key, origin, true, static v => SceneSourceCodeAsync(v), ct);

            if (result.Success == false)
                throw new Exception($"CachedWebJsSources: SceneSourceCodeAsync failed for url {path}: {result.Error!.Value.State} {result.Error!.Value.Message}");

            return result.Unwrap().Value.EnsureNotNull();
        }

        private static UniTask<string> SceneSourceCodeAsync((string key, IWebJsSources ctx, CancellationToken token) value) =>
            value.ctx!.SceneSourceCodeAsync(URLAddress.FromString(value.key!), value.token);

        private class DiskHashCompute : AbstractDiskHashCompute<string>
        {
            public static readonly DiskHashCompute INSTANCE = new ();

            protected override void FillPayload(IHashKeyPayload keyPayload, in string asset)
            {
                keyPayload.Put(asset);
            }
        }
    }
}
