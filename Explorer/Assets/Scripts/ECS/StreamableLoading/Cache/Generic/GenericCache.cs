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
        private readonly string extension;

        public GenericCache(IMemoryCache<T, TKey> memoryCache, IDiskCache<T> diskCache, string extension)
        {
            this.memoryCache = memoryCache;
            this.diskCache = diskCache;
            this.extension = extension;
        }

        public async UniTask<EnumResult<Option<T>, TaskError>> ContentAsync<TCtx>(
            TKey key,
            TCtx ctx,
            Func<(TKey key, TCtx ctx, CancellationToken token), UniTask<T>> fetchIfNotExists,
            CancellationToken token
        )
        {
            if (memoryCache.TryGet(key, out T result))
                return EnumResult<Option<T>, TaskError>.SuccessResult(Option<T>.Some(result));

            var diskResult = await diskCache.ContentAsync(key!.ToString()!, extension, token);

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

            try
            {
                var loadedContent = await fetchIfNotExists((key, ctx, token));

                memoryCache.Put(key, loadedContent);
                diskCache.PutAsync(key.ToString()!, extension, loadedContent, token).Forget();

                return EnumResult<Option<T>, TaskError>.SuccessResult(Option<T>.Some(loadedContent));
            }
            catch (Exception e) { return EnumResult<Option<T>, TaskError>.ErrorResult(TaskError.UnexpectedException, e.Message ?? string.Empty); }
        }
    }
}
