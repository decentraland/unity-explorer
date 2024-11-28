using CrdtEcsBridge.Components.Conversion;
using CrdtEcsBridge.Components.Transform;
using DCL.AvatarRendering.AvatarShape.Rendering.TextureArray;
using DCL.ECSComponents;
using DG.Tweening;
using DG.Tweening.Core;
using DG.Tweening.CustomPlugins;
using DG.Tweening.Plugins.Options;
using UnityEngine;

namespace DCL.SDKComponents.Tween.Components
{
    public class PositionTweener : CustomTweener<Vector3, VectorOptions>
    {
        protected override TweenerCore<Vector3, Vector3, VectorOptions> CreateTweener(Vector3 start, Vector3 end, float duration)
        {
            return DOTween.To(() => CurrentValue, x => CurrentValue = x, end, duration);
        }

        protected override (Vector3, Vector3) GetTweenValues(PBTween pbTween)
        {
            Vector3 start = PrimitivesConversionExtensions.PBVectorToUnityVector(pbTween.Move.Start);
            Vector3 end = PrimitivesConversionExtensions.PBVectorToUnityVector(pbTween.Move.End);
            CurrentValue = start;
            return (start, end);
        }

        // public override void UpdateComponent<T1>(ref T1 component)
        // {
        //     UpdateSDKTransform((SDKTransform)component);
        // }

        public override void UpdateSDKTransform(ref SDKTransform sdkTransform)
        {
            sdkTransform.Position.Value = CurrentValue;
        }

        public override void UpdateTransform(Transform transform)
        {
            transform.localPosition = CurrentValue;
        }
    }

    public class ScaleTweener : CustomTweener<Vector3, VectorOptions>
    {
        protected override TweenerCore<Vector3, Vector3, VectorOptions> CreateTweener(Vector3 start, Vector3 end, float duration)
        {
            return DOTween.To(() => CurrentValue, x => CurrentValue = x, end, duration);
        }

        protected override (Vector3, Vector3) GetTweenValues(PBTween pbTween)
        {
            Vector3 start = PrimitivesConversionExtensions.PBVectorToUnityVector(pbTween.Scale.Start);
            Vector3 end = PrimitivesConversionExtensions.PBVectorToUnityVector(pbTween.Scale.End);
            CurrentValue = start;
            return (start, end);
        }

        public override void UpdateSDKTransform(ref SDKTransform sdkTransform)
        {
            sdkTransform.Scale = CurrentValue;
        }

        public override void UpdateTransform(Transform transform)
        {
            transform.localScale = CurrentValue;
        }
    }

    public class RotationTweener : CustomTweener<Quaternion, NoOptions>
    {
        protected override (Quaternion, Quaternion) GetTweenValues(PBTween pbTween)
        {
            Quaternion start = PrimitivesConversionExtensions.PBQuaternionToUnityQuaternion(pbTween.Rotate.Start);
            Quaternion end = PrimitivesConversionExtensions.PBQuaternionToUnityQuaternion(pbTween.Rotate.End);
            CurrentValue = start;
            return (start, end);
        }

        protected override TweenerCore<Quaternion, Quaternion, NoOptions> CreateTweener(Quaternion start, Quaternion end, float duration)
        {
            return DOTween.To(PureQuaternionPlugin.Plug(), () => CurrentValue,
                x => CurrentValue = x,
                end, duration);
        }

        public override void UpdateSDKTransform(ref SDKTransform sdkTransform)
        {
            sdkTransform.Rotation.Value = CurrentValue;
        }

        public override void UpdateTransform(Transform transform)
        {
            transform.localRotation = CurrentValue;
        }
    }

    public class TextureMoveTweener : CustomTweener<Vector2, VectorOptions>
    {
        protected override TweenerCore<Vector2, Vector2, VectorOptions> CreateTweener(Vector2 start, Vector2 end, float duration) =>
            DOTween.To(() => CurrentValue, x => CurrentValue = x, end, duration);

        protected override (Vector2, Vector2) GetTweenValues(PBTween pbTween)
        {
            Vector2 start = pbTween.TextureMove.Start;
            Vector2 end = pbTween.TextureMove.End;
            CurrentValue = start;
            return (start, end);
        }

        // public override void UpdateMaterial(SDKTweenTextureComponent textureComponent, Material material)
        // {
        //     switch (textureComponent.TextureMoveMovementType)
        //     {
        //         case TextureMovementType.TmtOffset:
        //             material.SetTextureOffset(TextureArrayConstants.BASE_MAP_ORIGINAL_TEXTURE, CurrentValue);
        //             break;
        //         case TextureMovementType.TmtTiling:
        //             material.SetTextureScale(TextureArrayConstants.BASE_MAP_ORIGINAL_TEXTURE, CurrentValue);
        //             break;
        //     }
        // }
    }
}
