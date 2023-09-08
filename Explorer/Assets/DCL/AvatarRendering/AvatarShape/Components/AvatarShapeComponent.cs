using DCL.AvatarRendering.Wearables.Helpers;

namespace DCL.AvatarRendering.AvatarShape.Components
{
    public struct AvatarShapeComponent
    {
        public string ID;
        public WearablesLiterals.BodyShape BodyShape;

        public AvatarShapeComponent(string id, WearablesLiterals.BodyShape bodyShape)
        {
            ID = id;
            BodyShape = bodyShape;
        }
    }
}
