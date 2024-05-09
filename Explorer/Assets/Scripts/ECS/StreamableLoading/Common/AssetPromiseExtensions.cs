using Arch.Core;
using ECS.StreamableLoading.Common.Components;
using System;

namespace ECS.StreamableLoading.Common
{
    public static class AssetPromiseExtensions
    {
        /// <summary>
        ///     If the promise is already consumed returns the stored result,
        ///     otherwise consumes the promise and returns the result
        /// </summary>
        public static bool SafeTryConsume<TAsset, TLoadingIntention>(this ref AssetPromise<TAsset, TLoadingIntention> promise, World world, out StreamableLoadingResult<TAsset> result)
            where TLoadingIntention: IAssetIntention, IEquatable<TLoadingIntention>
        {
            if (promise.IsConsumed)
            {
                result = promise.Result
                         ?? new StreamableLoadingResult<TAsset>(new Exception("The promise generated no result"));
                return true;
            }

            return promise.TryConsume(world, out result);
        }
    }
}
