using CrdtEcsBridge.Components.Transform;
using DCL.AvatarRendering.AvatarShape.Rendering.TextureArray;
using DCL.ECSComponents;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace DCL.SDKComponents.Tween.Components
{
    public static class CustomTweenerExtensions
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void UpdateMaterial(this ITweener self, Material material, TextureMovementType movementType)
        {
            if (self is not Vector2Tweener vector2Tweener) return;

            switch (movementType)
            {
                case TextureMovementType.TmtOffset:
                    material.SetTextureOffset(TextureArrayConstants.BASE_MAP_ORIGINAL_TEXTURE, vector2Tweener.CurrentValue);
                    break;
                case TextureMovementType.TmtTiling:
                    material.SetTextureScale(TextureArrayConstants.BASE_MAP_ORIGINAL_TEXTURE, vector2Tweener.CurrentValue);
                    break;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void UpdateTransform(this ITweener self, Transform transform, PBTween.ModeOneofCase updateType)
        {
            switch (updateType)
            {
                case PBTween.ModeOneofCase.Move:
                case PBTween.ModeOneofCase.Scale:
                    if (self is not Vector3Tweener vector3Tweener) return;
                    if (updateType == PBTween.ModeOneofCase.Move)
                        transform.localPosition = vector3Tweener.CurrentValue;
                    else
                        transform.localScale = vector3Tweener.CurrentValue;
                    break;
                case PBTween.ModeOneofCase.Rotate:
                    if (self is not QuaternionTweener quaternionTweener) return;
                    transform.localRotation = quaternionTweener.CurrentValue;
                    break;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void UpdateSDKTransform(this ITweener self, ref SDKTransform sdkTransform, PBTween.ModeOneofCase updateType)
        {
            switch (updateType)
            {
                case PBTween.ModeOneofCase.Move:
                case PBTween.ModeOneofCase.Scale:
                    if (self is not Vector3Tweener vector3Tweener) return;
                    if (updateType == PBTween.ModeOneofCase.Move)
                        sdkTransform.Position.Value = vector3Tweener.CurrentValue;
                    else
                        sdkTransform.Scale = vector3Tweener.CurrentValue;
                    break;
                case PBTween.ModeOneofCase.Rotate:
                    if (self is not QuaternionTweener quaternionTweener) return;
                    sdkTransform.Rotation.Value = quaternionTweener.CurrentValue;
                    break;
            }
        }
    }
}
