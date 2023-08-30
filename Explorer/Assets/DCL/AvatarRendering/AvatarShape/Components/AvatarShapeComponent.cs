using Arch.Core;
using DCL.AvatarRendering.Wearables.Helpers;
using ECS.StreamableLoading.Common;

namespace DCL.AvatarRendering.AvatarShape.Components
{
    public struct AvatarShapeComponent
    {
        //LOADING
        public enum LifeCycle : byte
        {
            LoadingWearables = 0,
            LoadingAssetBundles = 1,
            LoadingFinished = 2,
        }

        public LifeCycle Status;

        public string ID;
        public EntityReference BodyShape;
        public EntityReference[] Wearables;

        public AssetPromise<WearableDTO[], GetWearableByPointersIntention> WearablePromise;
    }
}
