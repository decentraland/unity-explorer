using UnityEngine;

namespace DCL.AvatarRendering.Emotes
{
    /// <summary>
    ///     Activates an animation that makes the outline of an avatar blink several times.
    /// </summary>
    public struct PlayAvatarHighlightBlinkingAnimationIntent
    {
        public readonly float StartTime;
        public readonly float Thickness;
        public readonly Color OutlineColor;
        public readonly float Duration;
        public readonly int LoopCount;
        public float Progress;

        public PlayAvatarHighlightBlinkingAnimationIntent(float thickness, Color outlineColor, float duration, int loopCount)
        {
            StartTime = Time.time;
            Thickness = thickness;
            OutlineColor = outlineColor;
            Duration = duration;
            LoopCount = loopCount;
            Progress = 0.0f;
        }
    }
}