using Arch.Core;
using ECS.StreamableLoading.Common.Components;
using UnityEngine;
using Promise = ECS.StreamableLoading.Common.AssetPromise<UnityEngine.Texture2D, ECS.StreamableLoading.NFTShapes.GetNFTShapeIntention>;

namespace DCL.SDKComponents.NFTShape.Component
{
    public struct NFTLoadingComponent
    {
        private Promise promise;

        public NFTLoadingComponent(Promise promise)
        {
            this.promise = promise;
        }

        public bool TryGetResult(World world, out StreamableLoadingResult<Texture2D> result)
        {
            if (promise.IsConsumed)
            {
                result = default(StreamableLoadingResult<Texture2D>);
                return false;
            }

            return promise.TryConsume(world, out result);
        }

        public override readonly string ToString() =>
            $"NFTLoadingComponent {{ promise: {promise.Entity.Entity} {promise.LoadingIntention.CommonArguments.URL} }}";
    }
}
