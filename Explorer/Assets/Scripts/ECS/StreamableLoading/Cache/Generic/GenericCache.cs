using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using ECS.StreamableLoading.Cache.Disk;
using ECS.StreamableLoading.Cache.InMemory;
using System;
using System.Threading;
using Utility.Types;

namespace ECS.StreamableLoading.Cache.Generic
{
    public class GenericCache<T, TKey> : IGenericCache<T, TKey>
    {
        private readonly IMemoryCache<T, TKey> memoryCache;
        private readonly IDiskCache<T> diskCache;
        private readonly Func<TKey, string> stringifyFunc;
        private readonly string extension;

        public GenericCache(IMemoryCache<T, TKey> memoryCache, IDiskCache<T> diskCache, Func<TKey, string> stringifyFunc, string extension)
        {
            this.memoryCache = memoryCache;
            this.diskCache = diskCache;
            this.stringifyFunc = stringifyFunc;
            this.extension = extension;
        }

        public UniTask<EnumResult<TaskError>> PutAsync(TKey key, T value, CancellationToken token)
        {
            memoryCache.Put(key, value);
            return diskCache.PutAsync(stringifyFunc(key)!, extension, value, token);
        }

        public async UniTask<EnumResult<Option<T>, TaskError>> ContentAsync(TKey key, CancellationToken token)
        {
            if (memoryCache.TryGet(key, out T result))
                return EnumResult<Option<T>, TaskError>.SuccessResult(Option<T>.Some(result));

            string stringKey = stringifyFunc(key)!;

            var diskResult = await diskCache.ContentAsync(stringKey, extension, token);

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
