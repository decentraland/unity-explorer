using UnityEngine;

namespace DCL.AvatarRendering
{
    public interface IAvatarHighlightData
    {
        float OutlineVfxOpacity { get; }
        float OutlineThickness { get; }
        Color OutlineColor { get; }
        float FadeInTimeSeconds { get; }
        float FadeOutTimeSeconds { get; }
    }

    [CreateAssetMenu(fileName = "AvatarOutline", menuName = "DCL/Avatar/Avatar Outline Settings")]
    public class AvatarHighlightData : ScriptableObject, IAvatarHighlightData
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
