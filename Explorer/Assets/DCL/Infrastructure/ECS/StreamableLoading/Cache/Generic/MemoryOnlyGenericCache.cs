using Cysharp.Threading.Tasks;
using ECS.StreamableLoading.Cache.InMemory;
using System.Threading;
using Utility.Types;

namespace ECS.StreamableLoading.Cache.Generic
{
    public class MemoryOnlyGenericCache<T, TKey> : IGenericCache<T, TKey>
    {
        private readonly IMemoryCache<T, TKey> memoryCache;

        public MemoryOnlyGenericCache(IMemoryCache<T, TKey> memoryCache)
        {
            this.memoryCache = memoryCache;
        }

        public UniTask<EnumResult<TaskError>> PutAsync(TKey key, T value, bool qualifiedForDiskCache, CancellationToken token)
        {
            memoryCache.Put(key, value);
            return UniTask.FromResult(EnumResult<TaskError>.SuccessResult());
        }

        public UniTask<EnumResult<Option<T>, TaskError>> ContentAsync(TKey key, bool qualifiedForDiskCache, CancellationToken token) =>
            UniTask.FromResult(
                memoryCache.TryGet(key, out T result)
                    ? EnumResult<Option<T>, TaskError>.SuccessResult(Option<T>.Some(result))
                    : EnumResult<Option<T>, TaskError>.SuccessResult(Option<T>.None)
            );
    }
}
