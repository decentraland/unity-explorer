using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Optimization.Hashing;
using ECS.StreamableLoading.Cache.Disk;
using ECS.StreamableLoading.Cache.Disk.Cacheables;
using ECS.StreamableLoading.Cache.InMemory;
using System.Threading;
using Utility.Types;

namespace ECS.StreamableLoading.Cache.Generic
{
    public class GenericCache<T, TKey> : IGenericCache<T, TKey>
    {
        private readonly IMemoryCache<T, TKey> memoryCache;
        private readonly IDiskCache<T> diskCache;
        private readonly IDiskHashCompute<TKey> diskHashCompute;
        private readonly string extension;

        public GenericCache(IMemoryCache<T, TKey> memoryCache, IDiskCache<T> diskCache, IDiskHashCompute<TKey> diskHashCompute, string extension)
        {
            this.memoryCache = memoryCache;
            this.diskCache = diskCache;
            this.diskHashCompute = diskHashCompute;
            this.extension = extension;
        }

        public async UniTask<EnumResult<TaskError>> PutAsync(TKey key, T value, bool qualifiedForDiskCache, CancellationToken token)
        {
            memoryCache.Put(key, value);

            if (!qualifiedForDiskCache)
                return EnumResult<TaskError>.SuccessResult();

            using HashKey hashKey = diskHashCompute.ComputeHash(in key);
            return await diskCache.PutAsync(hashKey, extension, value, token);
        }

        public async UniTask<EnumResult<Option<T>, TaskError>> ContentAsync(TKey key, bool qualifiedForDiskCache, CancellationToken token)
        {
            if (memoryCache.TryGet(key, out T result))
                return EnumResult<Option<T>, TaskError>.SuccessResult(Option<T>.Some(result));

            if (!qualifiedForDiskCache)
                return EnumResult<Option<T>, TaskError>.SuccessResult(Option<T>.None);

            using HashKey hashKey = diskHashCompute.ComputeHash(in key);
            var diskResult = await diskCache.ContentAsync(hashKey, extension, token);

            if (diskResult.Success)
            {
                var option = diskResult.Value;

                if (option.Has)
                {
                    memoryCache.Put(key, option.Value);
                    return EnumResult<Option<T>, TaskError>.SuccessResult(Option<T>.Some(option.Value));
                }
            }
            else
                ReportHub.LogError(
                    ReportCategory.SCENE_LOADING,
                    $"Error getting disk cache content for '{key}' - {diskResult.Error!.Value.State} {diskResult.Error!.Value.Message}"
                );

            return EnumResult<Option<T>, TaskError>.SuccessResult(Option<T>.None);
        }
    }
}
