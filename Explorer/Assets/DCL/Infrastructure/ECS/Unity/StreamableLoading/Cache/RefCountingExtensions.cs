using Arch.Core;
using ECS.StreamableLoading.Common;
using ECS.StreamableLoading.Common.Components;
using System;

namespace ECS.StreamableLoading.Cache
{
    public static class RefCountingExtensions
    {
        public static void TryDereference<TData, TIntention>(this ref AssetPromise<TData, TIntention> texPromise, World world)
            where TData: IStreamableRefCountData
            where TIntention: ILoadingIntention, IEquatable<TIntention>
        {
            // texture should be released only if the result was created before
            if (texPromise.TryGetResult(world, out StreamableLoadingResult<TData> data) && data.Succeeded)
                data.Asset!.Dereference();
        }
    }
}
