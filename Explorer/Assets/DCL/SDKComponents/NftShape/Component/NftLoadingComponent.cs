using Arch.Core;
using ECS.StreamableLoading.Common.Components;
using UnityEngine;
using Promise = ECS.StreamableLoading.Common.AssetPromise<UnityEngine.Texture2D, ECS.StreamableLoading.NftShapes.GetNftShapeIntention>;

namespace DCL.SDKComponents.NftShape.Component
{
    public struct NftLoadingComponent
    {
        private Promise promise;

        public NftLoadingComponent(Promise promise)
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
    }
}
