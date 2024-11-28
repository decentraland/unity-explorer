using CrdtEcsBridge.Components.Transform;
using DCL.AvatarRendering.AvatarShape.Rendering.TextureArray;
using DCL.ECSComponents;
using DG.Tweening;
using UnityEngine;

namespace DCL.SDKComponents.Tween.Components
{
    public interface ICustomTweener<T>
    {
        T CurrentValue { get; set; }

        void Initialize(PBTween pbTween, float durationInSeconds);

        void DoTween(Ease ease, float tweenModelCurrentTime, bool isPlaying);

        void Play();

        void Pause();

        void Rewind();

        bool IsPaused();

        bool IsFinished();

        bool IsActive();

        void UpdateSDKTransform(ref SDKTransform sdkTransform);

        void UpdateTransform(Transform transform);

        // void UpdateComponent<T>(ref T component);
        // void UpdateComponent<T>(T component);

        //void UpdateMaterial(SDKTweenTextureComponent textureComponent, Material material);
    }

    public static class CustomTweenerExtensions
    {
        public static void UpdateMaterial(this ICustomTweener<Vector2> tweener, SDKTweenTextureComponent textureComponent, Material material)
        {
            switch (textureComponent.TextureMoveMovementType)
            {
                case TextureMovementType.TmtOffset:
                    material.SetTextureOffset(TextureArrayConstants.BASE_MAP_ORIGINAL_TEXTURE, tweener.CurrentValue);
                    break;
                case TextureMovementType.TmtTiling:
                    material.SetTextureScale(TextureArrayConstants.BASE_MAP_ORIGINAL_TEXTURE, tweener.CurrentValue);
                    break;
            }
        }


    }
}
