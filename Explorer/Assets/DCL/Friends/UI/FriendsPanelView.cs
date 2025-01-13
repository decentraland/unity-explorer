using MVC;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.Friends.UI
{
    public class FriendsPanelView: ViewBaseWithAnimationElement, IView
    {
        [field: SerializeField] public Button CloseButton { get; private set; }
        [field: SerializeField] public Button BackgroundCloseButton { get; private set; }
        [field: SerializeField] public NotificationIndicatorView NotificationIndicator { get; private set; }
        [field: SerializeField] public NotificationIndicatorView TabNotificationIndicator { get; private set; }

        [field: Header("Friends tab")]
        [field: SerializeField] public Button FriendsTabButton { get; private set; }
        [field: SerializeField] public GameObject FriendsTabSelected { get; private set; }
        [field: SerializeField] public GameObject FriendsPanel { get; private set; }

        [field: Header("Requests tab")]
        [field: SerializeField] public Button RequestsTabButton { get; private set; }
        [field: SerializeField] public GameObject RequestsTabSelected { get; private set; }
        [field: SerializeField] public GameObject RequestsPanel { get; private set; }

        [field: Header("Blocked tab")]
        [field: SerializeField] public Button BlockedTabButton { get; private set; }
        [field: SerializeField] public GameObject BlockedTabSelected { get; private set; }
        [field: SerializeField] public BlockedUsersView BlockedPanel { get; private set; }
    }
}
