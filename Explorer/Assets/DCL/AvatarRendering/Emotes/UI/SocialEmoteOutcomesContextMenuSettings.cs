using UnityEngine;

namespace DCL.SocialEmotes.UI
{
    [CreateAssetMenu(fileName = "NewSocialEmoteOutcomesContextMenuSettings", menuName = "DCL/Settings/SocialEmoteOutcomesContextMenuSettings")]
    public class SocialEmoteOutcomesContextMenuSettings : ScriptableObject
    {
        [SerializeField]
        private int width = 260;

        [SerializeField]
        private int elementsSpacing = 5;

        [SerializeField]
        private Vector2 offset = new (50, 0);

        [SerializeField]
        private RectOffset verticalLayoutPadding = new RectOffset(){left = 10, right = 10, top = 8, bottom = 16};

        [SerializeField]
        private Sprite? emoteIcon;

        public int Width => width;
        public int ElementsSpacing => elementsSpacing;
        public Vector2 Offset => offset;
        public RectOffset VerticalLayoutPadding => verticalLayoutPadding;
        public Sprite? EmoteIcon => emoteIcon;
    }
}
