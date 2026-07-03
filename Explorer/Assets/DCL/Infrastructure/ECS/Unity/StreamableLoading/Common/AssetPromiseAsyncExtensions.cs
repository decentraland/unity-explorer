using Arch.Core;
using Cysharp.Threading.Tasks;
using ECS.StreamableLoading.Common.Components;
using System;
using System.Threading;

namespace ECS.StreamableLoading.Common
{
    public static class AssetPromiseAsyncExtensions
    {
        /// <summary>
        ///     Wait and consume intention, leads to the entity removal
        /// </summary>
        public static async UniTask<AssetPromise<TAsset, TLoadingIntention>> ToUniTaskAsync<TAsset, TLoadingIntention>(this AssetPromise<TAsset, TLoadingIntention> promise,
            World world,
            PlayerLoopTiming playerLoopTiming = PlayerLoopTiming.Update,
            CancellationToken cancellationToken = default)
            where TLoadingIntention: IAssetIntention, IEquatable<TLoadingIntention>
        {
            do await UniTask.Yield(playerLoopTiming, cancellationToken);
            while (!promise.TryConsume(world, out _));

            // Return promise as it is modified
            return promise;
        }

        /// <summary>
        ///     Wait for the results to be ready, does not consume and does not destroy the entity
        /// </summary>
        public static async UniTask<AssetPromise<TAsset, TLoadingIntention>> ToUniTaskWithoutDestroyAsync<TAsset, TLoadingIntention>(this AssetPromise<TAsset, TLoadingIntention> promise,
            World world,
            PlayerLoopTiming playerLoopTiming = PlayerLoopTiming.Update,
            CancellationToken cancellationToken = default)
            where TLoadingIntention: IAssetIntention, IEquatable<TLoadingIntention>
        {
            do await UniTask.Yield(playerLoopTiming, cancellationToken);
            while (!promise.TryGetResult(world, out _));

            // Return promise as it is modified
            return promise;
        }
    }
}
