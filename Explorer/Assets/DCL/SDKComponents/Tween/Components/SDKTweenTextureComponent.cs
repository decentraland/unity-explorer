using DCL.ECSComponents;

namespace DCL.SDKComponents.Tween.Components
{
    public struct SDKTweenTextureComponent
    {
        public TextureMovementType TextureMoveMovementType { get; set; }

        public SDKTweenTextureComponent(TextureMovementType textureMoveMovementType)
        {
            TextureMoveMovementType = textureMoveMovementType;
        }
    }
}
