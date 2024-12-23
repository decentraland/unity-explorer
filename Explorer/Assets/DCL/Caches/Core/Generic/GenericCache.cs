using Cysharp.Threading.Tasks;
using DCL.Caches.Core.Disk;
using DCL.Diagnostics;
using System.Collections.Generic;
using System.Threading;

namespace DCL.Caches.Core.Generic
{
    public abstract class GenericCache<T> : IGenericCache<T> where T: class
    {
        private readonly Dictionary<string, T> memoryCache = new ();
        private readonly IDiskCache diskCache;

        protected GenericCache(IDiskCache diskCache)
        {
            this.diskCache = diskCache;
        }

        public async UniTask PutAsync(string key, T value, CancellationToken token)
        {
            lock (memoryCache) { memoryCache[key] = value; }

            byte[] data = await Serialize(value, token);
            await diskCache.PutAsync(key, ExtensionFor(key), data, token);
        }

        public async UniTask<T?> GetAsync(string key, CancellationToken token)
        {
            lock (memoryCache)
            {
                if (memoryCache.TryGetValue(key, out T? value))
                    return value;
            }

            var result = await diskCache.ContentAsync(key, ExtensionFor(key), token);

            if (result.Success == false)
            {
                ReportHub.LogError(
                    ReportCategory.GENERIC_CACHE,
                    $"Error getting cache content for '{key}' - {result.Error!.Value.State} {result.Error!.Value.Message}"
                );

                return null;
            }

            byte[]? data = result.Value;

            if (data == null)
                return null;

            T deserializedValue = await Deserialize(data, token)!;

            lock (memoryCache) { memoryCache[key] = deserializedValue; }

            return deserializedValue;
        }

        public async UniTask RemoveAsync(string key, CancellationToken token)
        {
            lock (memoryCache) { memoryCache.Remove(key); }

            await diskCache.RemoveAsync(key, ExtensionFor(key), token);
        }

        protected abstract UniTask<byte[]> Serialize(T value, CancellationToken token);

        protected abstract UniTask<T> Deserialize(byte[] data, CancellationToken token);

        protected abstract string ExtensionFor(string key);
    }
}
