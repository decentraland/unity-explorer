using Cysharp.Threading.Tasks;
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
        UniTask<EnumResult<Option<T>, TaskError>> ContentAsync<TCtx>(
            TKey key,
            TCtx ctx,
            Func<(TKey key, TCtx ctx, CancellationToken token), UniTask<T>> fetchIfNotExists,
            CancellationToken token
        );
    }
}
