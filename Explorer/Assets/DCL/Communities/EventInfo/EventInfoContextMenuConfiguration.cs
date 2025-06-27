using UnityEngine;

namespace DCL.Communities.EventInfo
{
    [CreateAssetMenu(fileName = "EventInfoContextMenuSettings", menuName = "DCL/Communities/EventInfo/ContextMenuSettings")]
    public class EventInfoContextMenuConfiguration : ScriptableObject
    {
        [SerializeField] private int contextMenuWidth = 250;
        [SerializeField] private int elementsSpacing = 5;
        [SerializeField] private RectOffset verticalPadding;
        [SerializeField] private Vector2 offsetFromTarget = Vector2.zero;
        [Space(10)]
        [SerializeField] private Sprite shareSprite;
        [SerializeField] private string shareText = "Share on X";
        [Space(10)]
        [SerializeField] private Sprite copyLinkSprite;
        [SerializeField] private string copyLinkText = "Copy link";

        public int ContextMenuWidth => contextMenuWidth;
        public int ElementsSpacing => elementsSpacing;
        public RectOffset VerticalPadding => verticalPadding;
        public Vector2 OffsetFromTarget => offsetFromTarget;

        public Sprite ShareSprite => shareSprite;
        public string ShareText => shareText;

        public Sprite CopyLinkSprite => copyLinkSprite;
        public string CopyLinkText => copyLinkText;
    }
}
