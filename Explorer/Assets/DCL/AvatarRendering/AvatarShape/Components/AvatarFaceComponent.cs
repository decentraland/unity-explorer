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