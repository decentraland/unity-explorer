using UnityEngine;

namespace DCL.Friends.UI.FriendPanel.Sections.Blocked
{
    public class BlockedSectionView : FriendPanelSectionView
    {
        [field: SerializeField] public BlockedUserContextMenuConfiguration ContextMenuSettings { get; private set; }
    }
}
