using DCL.AvatarRendering.Wearables.Helpers;
using Promise = ECS.StreamableLoading.Common.AssetPromise<DCL.AvatarRendering.Wearables.Components.Wearable[], DCL.AvatarRendering.Wearables.Components.Intentions.GetWearablesByPointersIntention>;

namespace DCL.AvatarRendering.AvatarShape.Components
{
    public struct AvatarShapeComponent
    {
        public string ID;
        public WearablesLiterals.BodyShape BodyShape;
        public bool IsDirty;

        public Promise WearablePromise;

        public AvatarShapeComponent(string id, WearablesLiterals.BodyShape bodyShape, Promise wearablePromise)
        {
            ID = id;
            BodyShape = bodyShape;
            IsDirty = true;
            WearablePromise = wearablePromise;
        }
    }
}
