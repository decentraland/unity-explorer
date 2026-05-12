using DCL.AvatarRendering.AvatarShape.Components;
using DCL.AvatarRendering.AvatarShape.Rendering.TextureArray;
using DCL.AvatarRendering.Loading.Assets;
using UnityEngine;

namespace DCL.AvatarRendering.AvatarShape
{
    /// <summary>
    ///     Material/MaterialPropertyBlock side-effects for face animation. The avatar facial
    ///     expression system delegates all expression-index binding here so it stays focused
    ///     on state transitions.
    /// </summary>
    public static class AvatarFaceMaterialUtils
    {
        private static readonly MaterialPropertyBlock MPB = new ();

        public static void ApplyEyebrowsFrame(ref AvatarFaceComponent face, int eyebrowsIndex)
        {
            if (face.EyebrowsRenderer == null) return;
            if (face.CurrentEyebrowsIndex == eyebrowsIndex) return;

            face.CurrentEyebrowsIndex = eyebrowsIndex;
            SetExpressionIndex(face.EyebrowsRenderer, eyebrowsIndex);
        }

        public static void ApplyEyeFrame(ref AvatarFaceComponent face, int eyeIndex)
        {
            if (face.EyeRenderer == null) return;
            if (face.CurrentEyeIndex == eyeIndex) return;

            face.CurrentEyeIndex = eyeIndex;
            SetExpressionIndex(face.EyeRenderer, eyeIndex);
        }

        public static void ApplyMouthPose(ref AvatarFaceComponent face, int mouthPoseIndex)
        {
            if (face.MouthRenderer == null) return;
            if (face.CurrentMouthPoseIndex == mouthPoseIndex) return;

            face.CurrentMouthPoseIndex = mouthPoseIndex;
            SetExpressionIndex(face.MouthRenderer, mouthPoseIndex);
        }

        public static void StartBlink(ref AvatarFaceComponent face)
        {
            face.IsBlinking = true;
            face.BlinkFrameIndex = 0;
            face.BlinkFrameTimer = 0f;
            ApplyEyeFrame(ref face, AvatarFacialExpressionConstants.BLINK_SEQUENCE[0]);
        }

        public static void EndBlink(ref AvatarFaceComponent face, float minBlinkInterval, float maxBlinkInterval)
        {
            face.IsBlinking = false;
            face.TimeSinceLastBlink = 0f;
            face.NextBlinkTime = Random.Range(minBlinkInterval, maxBlinkInterval);
            ApplyEyeFrame(ref face, face.EyesExpressionIndex);
        }

        public static void StopMouthAnimation(ref AvatarFaceComponent face)
        {
            face.AnimatingText = null;
            ApplyMouthPose(ref face, face.MouthExpressionIndex);
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

        // Sentinel index (<0) disables atlas slicing in the shader so non-atlas wearables sample
        // their full base map; >=0 picks one cell of the 4x4 atlas grid.
        private static void SetExpressionIndex(Renderer renderer, int index)
        {
            renderer.GetPropertyBlock(MPB);
            MPB.SetInteger(TextureArrayConstants.EXPRESSION_INDEX_SHADER, index);
            renderer.SetPropertyBlock(MPB);
        }
    }
}