using DCL.Diagnostics;
using MVC;
using SuperScrollView;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace DCL.Notifications.NotificationsMenu
{
    public class NotificationsMenuView : ViewBaseWithAnimationElement, IView, IPointerClickHandler
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

        // Swallow the click event so it's not processed by the main sidebar button: retriggers -> cancel previous token -> panel stuck
        public void OnPointerClick(PointerEventData eventData)
        {
            ReportHub.Log(ReportCategory.UI, "Swallowed click on view level");
        }
    }
}
