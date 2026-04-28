using DCL.AvatarRendering.AvatarShape.Components;
using DCL.AvatarRendering.AvatarShape.Rendering.TextureArray;
using DCL.AvatarRendering.Loading.Assets;
using UnityEngine;

namespace DCL.AvatarRendering.AvatarShape
{
    /// <summary>
    ///     Material/MaterialPropertyBlock side-effects for face animation. The avatar facial
    ///     expression system delegates all texture array binding here so it stays focused
    ///     on state transitions.
    /// </summary>
    public static class AvatarFaceMaterialUtils
    {
        private static readonly MaterialPropertyBlock MPB = new ();

        public static void ApplyEyebrowsFrame(ref AvatarFaceComponent face, int eyebrowsIndex, Texture2DArray? eyebrowsTextureArray)
        {
            if (face.EyebrowsRenderer == null || eyebrowsTextureArray == null) return;
            if (face.CurrentEyebrowsIndex == eyebrowsIndex) return;

            face.CurrentEyebrowsIndex = eyebrowsIndex;

            MPB.Clear();
            MPB.SetTexture(TextureArrayConstants.MAINTEX_ARR_TEX_SHADER, eyebrowsTextureArray);
            MPB.SetInteger(TextureArrayConstants.MAINTEX_ARR_SHADER_INDEX, eyebrowsIndex);
            face.EyebrowsRenderer.SetPropertyBlock(MPB);
        }

        public static void ApplyEyeFrame(ref AvatarFaceComponent face, int eyeIndex, Texture2DArray? eyeTextureArray)
        {
            if (face.EyeRenderer == null) return;
            if (face.CurrentEyeIndex == eyeIndex) return;

            face.CurrentEyeIndex = eyeIndex;

            if (eyeIndex == AvatarFacialExpressionConstants.NO_EYE_OVERRIDE)
            {
                face.EyeRenderer.SetPropertyBlock(null);
                return;
            }

            if (eyeTextureArray == null) return;

            MPB.Clear();
            MPB.SetTexture(TextureArrayConstants.MAINTEX_ARR_TEX_SHADER, eyeTextureArray);
            MPB.SetInteger(TextureArrayConstants.MAINTEX_ARR_SHADER_INDEX, eyeIndex);
            face.EyeRenderer.SetPropertyBlock(MPB);
        }

        public static void ApplyMouthPose(ref AvatarFaceComponent face, int mouthPoseIndex, Texture2DArray? mouthPoseTextureArray)
        {
            if (face.MouthRenderer == null) return;
            if (face.CurrentMouthPoseIndex == mouthPoseIndex) return;

            face.CurrentMouthPoseIndex = mouthPoseIndex;

            if (mouthPoseIndex == AvatarFacialExpressionConstants.NO_MOUTH_POSE)
            {
                face.MouthRenderer.SetPropertyBlock(null);
                return;
            }

            if (mouthPoseTextureArray == null) return;

            MPB.Clear();
            MPB.SetTexture(TextureArrayConstants.MAINTEX_ARR_TEX_SHADER, mouthPoseTextureArray);
            MPB.SetInteger(TextureArrayConstants.MAINTEX_ARR_SHADER_INDEX, mouthPoseIndex);
            face.MouthRenderer.SetPropertyBlock(MPB);
        }

        public static void StartBlink(ref AvatarFaceComponent face, Texture2DArray? eyeTextureArray)
        {
            face.IsBlinking = true;
            face.BlinkFrameIndex = 0;
            face.BlinkFrameTimer = 0f;
            ApplyEyeFrame(ref face, AvatarFacialExpressionConstants.BLINK_SEQUENCE[0], eyeTextureArray);
        }

        public static void EndBlink(ref AvatarFaceComponent face, Texture2DArray? eyeTextureArray, float minBlinkInterval, float maxBlinkInterval)
        {
            face.IsBlinking = false;
            face.TimeSinceLastBlink = 0f;
            face.NextBlinkTime = Random.Range(minBlinkInterval, maxBlinkInterval);
            ApplyEyeFrame(ref face, face.EyesExpressionIndex, eyeTextureArray);
        }

        public static void StopMouthAnimation(ref AvatarFaceComponent face, Texture2DArray? mouthPoseTextureArray)
        {
            face.AnimatingText = null;
            ApplyMouthPose(ref face, face.MouthExpressionIndex, mouthPoseTextureArray);
        }

        /// <summary>
        ///     Searches the avatar's instantiated wearables for the first renderer whose name ends
        ///     with <paramref name="suffix"/>. Returns null when none of the wearables expose one
        ///     (e.g. wearable lacks a Mask_* mesh).
        /// </summary>
        public static Renderer? FindRendererWithSuffix(in AvatarShapeComponent avatarShape, string suffix)
        {
            for (var i = 0; i < avatarShape.InstantiatedWearables.Count; i++)
            {
                CachedAttachment wearable = avatarShape.InstantiatedWearables[i];

                for (var j = 0; j < wearable.Renderers.Count; j++)
                {
                    Renderer renderer = wearable.Renderers[j];

                    if (renderer.name.EndsWith(suffix))
                        return renderer;
                }
            }

            return null;
        }
    }
}