
using DCL.Chat;
using UnityEngine;

namespace DCL.Friends.UI.FriendPanel.Sections.Friends
{
    public class FriendsSectionView : FriendPanelSectionView
    {
        [field: SerializeField] public ChatEntryConfigurationSO ChatEntryConfiguration { get; private set; }
        [field: SerializeField] public FriendListContextMenuConfiguration ContextMenuSettings { get; private set; }
    }
}
