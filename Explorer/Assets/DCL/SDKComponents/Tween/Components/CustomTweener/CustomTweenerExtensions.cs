using CrdtEcsBridge.Components.Transform;
using DCL.AvatarRendering.AvatarShape.Rendering.TextureArray;
using DCL.ECSComponents;
using UnityEngine;

namespace DCL.SDKComponents.Tween.Components
{
    public static class CustomTweenerExtensions
    {
        public static void UpdateMaterial(this ITweener self, Material material, TextureMovementType movementType)
        {
            switch (movementType)
            {
                case TextureMovementType.TmtOffset:
                    material.SetTextureOffset(TextureArrayConstants.BASE_MAP_ORIGINAL_TEXTURE, ((ICustomTweener<Vector2>)self).CurrentValue);
                    break;
                case TextureMovementType.TmtTiling:
                    material.SetTextureScale(TextureArrayConstants.BASE_MAP_ORIGINAL_TEXTURE, ((ICustomTweener<Vector2>)self).CurrentValue);
                    break;
            }
        }

        public static void UpdateTransform(this ITweener self, Transform transform, PBTween.ModeOneofCase updateType)
        {
            switch (updateType)
            {
                case PBTween.ModeOneofCase.Move:
                    transform.localPosition = ((ICustomTweener<Vector3>)self).CurrentValue;
                    break;
                case PBTween.ModeOneofCase.Scale:
                    transform.localScale = ((ICustomTweener<Vector3>)self).CurrentValue;
                    break;
                case PBTween.ModeOneofCase.Rotate:
                    transform.localRotation = ((ICustomTweener<Quaternion>)self).CurrentValue;
                    break;
            }
        }

        public static void UpdateSDKTransform(this ITweener self, ref SDKTransform sdkTransform, PBTween.ModeOneofCase updateType)
        {
            switch (updateType)
            {
                case PBTween.ModeOneofCase.Move:
                    sdkTransform.Position.Value = ((ICustomTweener<Vector3>)self).CurrentValue;
                    break;
                case PBTween.ModeOneofCase.Scale:
                    sdkTransform.Scale = ((ICustomTweener<Vector3>)self).CurrentValue;
                    break;
                case PBTween.ModeOneofCase.Rotate:
                    sdkTransform.Rotation.Value = ((ICustomTweener<Quaternion>)self).CurrentValue;
                    break;
            }
        }
    }
}
