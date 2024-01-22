using Arch.Core;
using ECS.StreamableLoading;
using ECS.StreamableLoading.Common.Components;
using UnityEngine;
using Promise = ECS.StreamableLoading.Common.AssetPromise<UnityEngine.Texture2D, ECS.StreamableLoading.NftShapes.GetNftShapeIntention>;

namespace DCL.SDKComponents.NftShape.Component
{
    public struct NftLoadingComponent
    {
        private Promise promise;
        private LifeCycle status;

        public NftLoadingComponent(Promise promise)
        {
            this.promise = promise;
            status = LifeCycle.LoadingInProgress;
        }

        public void Finish()
        {
            status = LifeCycle.LoadingFinished;
        }

        public void Applied()
        {
            status = LifeCycle.Applied;
        }

        public bool TryGetResult(World world, out StreamableLoadingResult<Texture2D> result)
        {
            if (status is LifeCycle.LoadingInProgress)
                return promise.TryGetResult(world, out result);

            result = default(StreamableLoadingResult<Texture2D>);
            return false;
        }
    }
}
