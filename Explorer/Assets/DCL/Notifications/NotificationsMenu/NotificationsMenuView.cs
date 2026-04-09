using MVC;
using SuperScrollView;
using TMPro;
using UnityEngine;

namespace DCL.Notifications.NotificationsMenu
{
    public class NotificationsMenuView : ViewBaseWithAnimationElement, IView
    {
        [field: SerializeField]
        public LoopListView2 LoopList { get; private set; }

        [field: SerializeField]
        public TMP_Text unreadNotificationCounterText { get; private set; } = null!;

        [field: SerializeField]
        public GameObject notificationIndicator { get; private set; }

        [field: SerializeField]
        public GameObject LoadingSpinner { get; private set; } = null!;

        [field: SerializeField]
        public GameObject ContentContainer { get; private set; } = null!;

        public void SetLoading(bool isLoading)
        {
            LoadingSpinner.SetActive(isLoading);
            ContentContainer.SetActive(!isLoading);
        }
    }
}
