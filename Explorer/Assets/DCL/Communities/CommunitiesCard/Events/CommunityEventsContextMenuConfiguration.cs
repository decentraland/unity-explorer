using UnityEngine;

namespace DCL.Communities.CommunitiesCard.Events
{
    [CreateAssetMenu(fileName = "CommunityEventsContextMenuSettings", menuName = "DCL/Communities/Events/ContextMenuSettings")]
    public class CommunityEventsContextMenuConfiguration : ScriptableObject
    {
        [field: SerializeField] public int ContextMenuWidth { get; private set; } = 180;
        [field: SerializeField] public int ElementsSpacing { get; private set; } = 5;
        [field: SerializeField] public RectOffset VerticalPadding { get; private set; }

        [field: SerializeField] public Sprite ShareSprite { get; private set; }
        [field: SerializeField] public string ShareText { get; private set; } = "Share on X";

        [field: SerializeField] public Sprite CopyLinkSprite { get; private set; }
        [field: SerializeField] public string CopyLinkText { get; private set; } = "Copy link";
    }
}
