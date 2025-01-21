
using DCL.Chat;
using UnityEngine;

namespace DCL.Friends.UI.FriendPanel.Sections.Friends
{
    public class FriendsSectionView : FriendPanelSectionView
    {
        [field: SerializeField] public ChatEntryConfigurationSO ChatEntryConfiguration { get; private set; }
        [field: SerializeField] public int ContextMenuWidth { get; private set; } = 250;

        [field: Header("Context menu assets")]
        [field: SerializeField] public Sprite ContextMenuViewProfileSprite { get; private set; }
        [field: SerializeField] public string ContextMenuViewProfileText { get; private set; }
        [field: Space(10)]
        [field: SerializeField] public Sprite ContextMenuChatSprite { get; private set; }
        [field: SerializeField] public string ContextMenuChatText { get; private set; }
        [field: Space(10)]
        [field: SerializeField] public Sprite ContextMenuJumpInSprite { get; private set; }
        [field: SerializeField] public string ContextMenuJumpInText { get; private set; }
        [field: Space(10)]
        [field: SerializeField] public Sprite ContextMenuBlockSprite { get; private set; }
        [field: SerializeField] public string ContextMenuBlockText { get; private set; }
        [field: Space(10)]
        [field: SerializeField] public Sprite ContextMenuReportSprite { get; private set; }
        [field: SerializeField] public string ContextMenuReportText { get; private set; }
    }
}
