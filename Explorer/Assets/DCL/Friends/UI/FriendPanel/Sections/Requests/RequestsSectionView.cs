using UnityEngine;

namespace DCL.Friends.UI.FriendPanel.Sections.Requests
{
    public class RequestsSectionView : FriendPanelSectionView
    {
        [field: SerializeField] public NotificationIndicatorView TabNotificationIndicator { get; private set; }
        [field: SerializeField] public FriendRequestContextMenuConfiguration ContextMenuSettings { get; private set; }
    }
}
