using DCL.Diagnostics;
using MVC;
using SuperScrollView;
using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

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

        [field: SerializeField]
        public GameObject EmptyState { get; private set; } = null!;

        [field: SerializeField]
        private Button foundationCommunityButton { get; set; } = null!;

        public event Action? FoundationCommunityButtonClicked;

        private void Awake()
        {
            foundationCommunityButton.onClick.AddListener(OnFoundationCommunityButtonClick);
        }

        private void OnDestroy()
        {
            foundationCommunityButton.onClick.RemoveListener(OnFoundationCommunityButtonClick);
        }

        private void OnFoundationCommunityButtonClick() => FoundationCommunityButtonClicked?.Invoke();

        public void SetLoading(bool isLoading)
        {
            LoadingSpinner.SetActive(isLoading);
            ContentContainer.SetActive(!isLoading);
        }

        public void ShowEmptyState(bool show) => EmptyState.SetActive(show);

        // Swallow the click event so it's not processed by the main sidebar button: retriggers -> cancel previous token -> panel stuck
        public void OnPointerClick(PointerEventData eventData)
        {
#if UNITY_EDITOR
            ReportHub.Log(ReportCategory.UI, "Swallowed click on view level");
#endif
        }
    }
}
