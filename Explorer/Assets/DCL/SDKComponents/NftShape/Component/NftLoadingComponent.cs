using ECS.StreamableLoading;
using Promise = ECS.StreamableLoading.Common.AssetPromise<UnityEngine.Texture2D, ECS.StreamableLoading.NftShapes.GetNftShapeIntention>;

namespace DCL.SDKComponents.NftShape.Component
{
    public struct NftLoadingComponent
    {
        public Promise promise;
        public LifeCycle readOnlyStatus;

        public NftLoadingComponent(Promise promise)
        {
            this.promise = promise;
            readOnlyStatus = LifeCycle.LoadingInProgress;
        }

        public void Finish()
        {
            readOnlyStatus = LifeCycle.LoadingFinished;
        }

        public void Applied()
        {
            readOnlyStatus = LifeCycle.Applied;
        }
    }
}
