namespace DCL.AvatarRendering.AvatarShape.Components
{
    public struct AvatarShapeComponent
    {
        public bool IsDirty;
        public string ID;
        public string BodyShape;

        public AvatarShapeComponent(string id, string bodyShape)
        {
            ID = id;
            BodyShape = bodyShape;
            IsDirty = true;
        }
    }
}
