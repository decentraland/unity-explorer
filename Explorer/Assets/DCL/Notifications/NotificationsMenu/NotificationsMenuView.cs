using MVC;
using SuperScrollView;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.Notifications.NotificationsMenu
{
    public class NotificationsMenuView : ViewBaseWithAnimationElement
    {
        [field: SerializeField]
        public LoopListView2 LoopList { get; private set; }

        [field: SerializeField]
        public Button CloseButton { get; private set; } = null!;

        [field: SerializeField]
        public TMP_Text unreadNotificationCounterText { get; private set; } = null!;

        [field: SerializeField]
        public GameObject notificationIndicator { get; private set; }
    }
}
