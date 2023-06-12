using Arch.Core;
using Cysharp.Threading.Tasks;
using ECS.StreamableLoading.Common.Components;
using System.Threading;

namespace ECS.StreamableLoading.Common
{
    public static class AssetPromiseAsyncExtensions
    {
        /// <summary>
        ///     Wait and consume intention, leads to the entity removal
        /// </summary>
        public static async UniTask<StreamableLoadingResult<TAsset>> ToUniTask<TAsset, TLoadingIntention>(this AssetPromise<TAsset, TLoadingIntention> promise,
            World world,
            PlayerLoopTiming playerLoopTiming = PlayerLoopTiming.Update,
            CancellationToken cancellationToken = default)
            where TLoadingIntention: ILoadingIntention
        {
            StreamableLoadingResult<TAsset> result;

            do await UniTask.Yield(playerLoopTiming, cancellationToken);
            while (!promise.TryConsume(world, out result));

            return result;
        }
    }
}
