using CrdtEcsBridge.Components.Transform;
using DCL.AvatarRendering.AvatarShape.Rendering.TextureArray;
using DCL.ECSComponents;
using DCL.SDKComponents.Tween.Components;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace DCL.SDKComponents.Tween
{
    public static class CustomTweenerExtensions
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void UpdateMaterial(this ITweener self, Material material, TextureMovementType movementType)
        {
            if (self is not Vector2Tweener vector2Tweener) return;

            var value = vector2Tweener.CurrentValue;

            switch (movementType)
            {
                case TextureMovementType.TmtOffset:
                    material.SetTextureOffset(TextureArrayConstants.BASE_MAP_ORIGINAL_TEXTURE, value);
                    break;
                case TextureMovementType.TmtTiling:
                    material.SetTextureScale(TextureArrayConstants.BASE_MAP_ORIGINAL_TEXTURE, value);
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
                case PBTween.ModeOneofCase.MoveContinuous:
                    if (self is not Vector3Tweener vector3Tweener) return;
                    var value3 = vector3Tweener.CurrentValue;
                    if (updateType == PBTween.ModeOneofCase.Move || updateType == PBTween.ModeOneofCase.MoveContinuous)
                        transform.localPosition = value3;
                    else
                        transform.localScale = value3;
                    break;
                case PBTween.ModeOneofCase.Rotate:
                case PBTween.ModeOneofCase.RotateContinuous:
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
                case PBTween.ModeOneofCase.MoveContinuous:
                    if (self is not Vector3Tweener vector3Tweener) return;
                    var value3 = vector3Tweener.CurrentValue;
                    if (updateType == PBTween.ModeOneofCase.Move || updateType == PBTween.ModeOneofCase.MoveContinuous)
                        sdkTransform.Position.Value = value3;
                    else
                        sdkTransform.Scale = value3;
                    break;
                case PBTween.ModeOneofCase.Rotate:
                case PBTween.ModeOneofCase.RotateContinuous:
                    if (self is not QuaternionTweener quaternionTweener) return;
                    sdkTransform.Rotation.Value = quaternionTweener.CurrentValue;
                    break;
            }
        }
    }
}
