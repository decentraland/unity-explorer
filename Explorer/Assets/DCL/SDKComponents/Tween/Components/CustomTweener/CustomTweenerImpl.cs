using CrdtEcsBridge.Components.Conversion;
using CrdtEcsBridge.Components.Transform;
using DCL.ECSComponents;
using DG.Tweening;
using DG.Tweening.Core;
using DG.Tweening.CustomPlugins;
using DG.Tweening.Plugins.Options;
using ECS.Unity.Textures.Components.Extensions;
using UnityEngine;

namespace DCL.SDKComponents.Tween.Components
{
    public class PositionTweener : CustomTweener<Vector3, VectorOptions>
    {
        protected override TweenerCore<Vector3, Vector3, VectorOptions> CreateTweener(Vector3 start, Vector3 end, float duration)
        {
            return DOTween.To(() => currentValue, x => currentValue = x, end, duration);
        }

        protected override (Vector3, Vector3) GetTweenValues(PBTween pbTween)
        {
            Vector3 start = PrimitivesConversionExtensions.PBVectorToUnityVector(pbTween.Move.Start);
            Vector3 end = PrimitivesConversionExtensions.PBVectorToUnityVector(pbTween.Move.End);
            currentValue = start;
            return (start, end);
        }

        public override void UpdateSDKTransform(ref SDKTransform sdkTransform)
        {
            sdkTransform.Position.Value = currentValue;
        }

        public override void UpdateTransform(Transform transform)
        {
            transform.localPosition = currentValue;
        }

        public override void UpdateMaterial(Material _) { }
    }

    public class ScaleTweener : CustomTweener<Vector3, VectorOptions>
    {
        protected override TweenerCore<Vector3, Vector3, VectorOptions> CreateTweener(Vector3 start, Vector3 end, float duration)
        {
            return DOTween.To(() => currentValue, x => currentValue = x, end, duration);
        }

        protected override (Vector3, Vector3) GetTweenValues(PBTween pbTween)
        {
            Vector3 start = PrimitivesConversionExtensions.PBVectorToUnityVector(pbTween.Scale.Start);
            Vector3 end = PrimitivesConversionExtensions.PBVectorToUnityVector(pbTween.Scale.End);
            currentValue = start;
            return (start, end);
        }

        public override void UpdateSDKTransform(ref SDKTransform sdkTransform)
        {
            sdkTransform.Scale = currentValue;
        }

        public override void UpdateTransform(Transform transform)
        {
            transform.localScale = currentValue;
        }

        public override void UpdateMaterial(Material _) { }
    }

    public class RotationTweener : CustomTweener<Quaternion, NoOptions>
    {
        protected override (Quaternion, Quaternion) GetTweenValues(PBTween pbTween)
        {
            Quaternion start = PrimitivesConversionExtensions.PBQuaternionToUnityQuaternion(pbTween.Rotate.Start);
            Quaternion end = PrimitivesConversionExtensions.PBQuaternionToUnityQuaternion(pbTween.Rotate.End);
            currentValue = start;
            return (start, end);
        }

        protected override TweenerCore<Quaternion, Quaternion, NoOptions> CreateTweener(Quaternion start, Quaternion end, float duration)
        {
            return DOTween.To(PureQuaternionPlugin.Plug(), () => currentValue,
                x => currentValue = x,
                end, duration);
        }

        public override void UpdateSDKTransform(ref SDKTransform sdkTransform)
        {
            sdkTransform.Rotation.Value = currentValue;
        }

        public override void UpdateTransform(Transform transform)
        {
            transform.localRotation = currentValue;
        }

        public override void UpdateMaterial(Material _) { }
    }

    public class TextureMoveTweener : CustomTweener<Vector2, VectorOptions>
    {
        protected override TweenerCore<Vector2, Vector2, VectorOptions> CreateTweener(Vector2 start, Vector2 end, float duration) =>
            DOTween.To(() => currentValue, x => currentValue = x, end, duration);

        protected override (Vector2, Vector2) GetTweenValues(PBTween pbTween)
        {
            Vector2 start = pbTween.TextureMove.Start.ToUnityVector2();
            Vector2 end = pbTween.TextureMove.End.ToUnityVector2();
            currentValue = start;
            return (start, end);
        }

        public override void UpdateSDKTransform(ref SDKTransform _) { }

        public override void UpdateTransform(Transform _) { }

        public override void UpdateMaterial(Material material)
        {
            material.SetTextureOffset(Shader.PropertyToID("_BaseMap"), currentValue);
        }
    }
}
