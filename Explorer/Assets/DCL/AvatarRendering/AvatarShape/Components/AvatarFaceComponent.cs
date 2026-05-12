using UnityEngine;

namespace DCL.AvatarRendering.AvatarShape.Components
{
    /// <summary>
    ///     Per-avatar facial animation state. One component holds all three layers because they are
    ///     always added/removed together and the pipeline reads them together each frame:
    ///     base expression → blink overrides eyes → chat/voice overrides mouth → pause restores
    ///     mouth to <see cref="MouthExpressionIndex"/> (not idle).
    /// </summary>
    public struct AvatarFaceComponent
    {
        // Renderers — null while setup is pending or after avatar re-instantiation.
        public Renderer EyebrowsRenderer;
        public Renderer EyeRenderer;
        public Renderer MouthRenderer;

        // Cached <see cref="Renderer.sharedMaterial"/> at the time we last wrote _ExpressionIndex.
        // The skinning material pool can swap the material under us during a wearable swap while
        // returning the SAME renderer instance from the wearable cache — the new material starts at
        // the shader-default sentinel, so a renderer-ref-only diff would happily skip the write and
        // the channel would render the full atlas. AvatarFacialExpressionSystem diffs these against
        // <c>renderer.sharedMaterial</c> each frame to detect that case.
        public Material EyebrowsMaterial;
        public Material EyeMaterial;
        public Material MouthMaterial;

        // Per-channel atlas capability of the currently worn wearable. Stable across animation;
        // only changes when a wearable swap rebinds the face renderers.
        public bool EyebrowsHasExpressionAtlas;
        public bool EyesHasExpressionAtlas;
        public bool MouthHasExpressionAtlas;

        // Resting atlas cell per channel (0..N when capability bool is true, -1 otherwise).
        // Eyes/mouth are restored to these when blink / mouth animation ends.
        public int EyebrowsExpressionIndex;
        public int EyesExpressionIndex;
        public int MouthExpressionIndex;

        // Currently applied MaterialPropertyBlock slice indices. -1 means no override (material default).
        public int CurrentEyebrowsIndex;
        public int CurrentEyeIndex;
        public int CurrentMouthPoseIndex;

        // Blink state.
        public bool IsBlinking;
        public float TimeSinceLastBlink;
        public float NextBlinkTime;
        public int BlinkFrameIndex;
        public float BlinkFrameTimer;

        // Mouth-pose animation state. AnimatingText is null when idle.
        public string? AnimatingText;
        public int CharacterIndex;
        public float CharacterTimer;

        /// <summary>True when expression indices changed and need to be pushed to renderers this frame.</summary>
        public bool IsDirty;
    }
}