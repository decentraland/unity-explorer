using UnityEngine;

namespace DCL.AvatarRendering
{
    public readonly struct ReadOnlyAvatarHighlightData
    {
        public readonly float OutlineVfxOpacity;
        public readonly float OutlineThickness;
        public readonly Color OutlineColor;
        public readonly float FadeInTimeSeconds;
        public readonly float FadeOutTimeSeconds;

        public ReadOnlyAvatarHighlightData(AvatarHighlightData data) : this(data.OutlineVfxOpacity, data.OutlineThickness, data.OutlineColor, data.FadeInTimeSeconds, data.FadeOutTimeSeconds) { }
        public ReadOnlyAvatarHighlightData(float outlineVfxOpacity, float outlineThickness, Color outlineColor, float fadeInTimeSeconds, float fadeOutTimeSeconds)
        {
            OutlineVfxOpacity = outlineVfxOpacity;
            OutlineThickness = outlineThickness;
            OutlineColor = outlineColor;
            FadeInTimeSeconds = fadeInTimeSeconds;
            FadeOutTimeSeconds = fadeOutTimeSeconds;
        }
    }

    [CreateAssetMenu(fileName = "AvatarOutline", menuName = "DCL/Avatar/Avatar Outline Settings")]
    public class AvatarHighlightData : ScriptableObject
    {
        [field: SerializeField]
        public float OutlineVfxOpacity { get; set; }

        [field: SerializeField]
        public float OutlineThickness { get; set; }

        [field: SerializeField]
        public Color OutlineColor { get; set; }

        [field: SerializeField]
        public float FadeInTimeSeconds { get; set; }

        [field: SerializeField]
        public float FadeOutTimeSeconds { get; set; }
    }
}
