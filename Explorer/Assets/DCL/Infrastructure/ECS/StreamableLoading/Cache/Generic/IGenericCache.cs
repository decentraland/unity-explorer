using Cysharp.Threading.Tasks;
using ECS.StreamableLoading.Cache.InMemory;
using System;
using System.Threading;
using Utility.Types;

namespace ECS.StreamableLoading.Cache.Generic
{
    /// <summary>
    /// Generic cache uses memory and disk to store and retrieve cached data
    /// </summary>
    public interface IGenericCache<T, TKey>
    {
        UniTask<EnumResult<TaskError>> PutAsync(TKey key, T value, bool qualifiedForDiskCache, CancellationToken token);

        UniTask<EnumResult<Option<T>, TaskError>> ContentAsync(TKey key, bool qualifiedForDiskCache, CancellationToken token);
    }

    public static class GenericCacheExtensions
    {
        public static async UniTask<EnumResult<Option<T>, TaskError>> ContentOrFetchAsync<T, TKey, TCtx>(
            this IGenericCache<T, TKey> cache,
            TKey key,
            TCtx ctx,
            bool qualifiedForDiskCache,
            Func<(TKey key, TCtx ctx, CancellationToken token), UniTask<T>> fetchIfNotExists,
            CancellationToken token
        )
        {
            EnumResult<Option<T>, TaskError> result = await cache.ContentAsync(key, qualifiedForDiskCache, token);

            if (result.Success == false || result.Value.Has)
                return result;

            try
            {
                var loadedContent = await fetchIfNotExists((key, ctx, token));
                await cache.PutAsync(key, loadedContent, qualifiedForDiskCache, token);
                return EnumResult<Option<T>, TaskError>.SuccessResult(Option<T>.Some(loadedContent));
            }
            catch (Exception e) { return EnumResult<Option<T>, TaskError>.ErrorResult(TaskError.UnexpectedException, e.Message ?? string.Empty); }
        }
    }
}
