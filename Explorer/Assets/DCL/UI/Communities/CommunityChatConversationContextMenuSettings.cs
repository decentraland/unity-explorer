using System;
using UnityEngine;
using UnityEngine.Serialization;

namespace DCL.UI.Communities
{
    [CreateAssetMenu(fileName = "CommunityChatConversationContextMenuSettings", menuName = "DCL/Communities/CommunityChatConversationContextMenuSettings")]
    [Serializable]
    public class CommunityChatConversationContextMenuSettings : ScriptableObject
    {
        [Header("Layout")]
        [SerializeField]
        private int width = 210;

        [SerializeField]
        private int elementsSpacing = 8;

        [SerializeField]
        private Vector2 offset = new (0, 100);

        [SerializeField]
        private RectOffset verticalLayoutPadding;

        [Header("Contents")]
        [SerializeField]
        private Sprite viewCommunitySprite;

        [SerializeField]
        private string viewCommunityText = "View Community";

        public Sprite ViewCommunitySprite => viewCommunitySprite;
        public string ViewCommunityText => viewCommunityText;

        public int Width => width;
        public int ElementsSpacing => elementsSpacing;
        public Vector2 Offset => offset;
        public RectOffset VerticalLayoutPadding => verticalLayoutPadding;
    }
}
