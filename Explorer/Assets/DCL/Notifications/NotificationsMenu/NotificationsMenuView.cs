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
        private (Transform? parent, int siblingIndex, Vector2 anchorMin, Vector2 anchorMax,
            Vector2 pivot, Vector2 position)? sidebarPlacement;

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

        /// <summary>Places the popup below a header button, or restores its sidebar placement.</summary>
        public void SetAnchor(RectTransform? anchor)
        {
            var rect = (RectTransform)transform;
            if (anchor == null)
            {
                if (sidebarPlacement is not { } original)
                    return;

                rect.SetParent(original.parent, false);
                rect.SetSiblingIndex(original.siblingIndex);
                rect.anchorMin = original.anchorMin;
                rect.anchorMax = original.anchorMax;
                rect.pivot = original.pivot;
                rect.anchoredPosition = original.position;
                sidebarPlacement = null;
                return;
            }

            sidebarPlacement ??= (rect.parent, rect.GetSiblingIndex(), rect.anchorMin, rect.anchorMax, rect.pivot, rect.anchoredPosition);
            rect.SetParent(anchor, false);
            rect.anchorMin = rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = Vector2.one;
            rect.anchoredPosition = new Vector2(0f, -12f);
        }

        private void Awake()
        {
            foundationCommunityButton.onClick.AddListener(OnFoundationCommunityButtonClick);
            OnViewHidden += RestoreSidebarPlacement;
        }

        private void OnDestroy()
        {
            foundationCommunityButton.onClick.RemoveListener(OnFoundationCommunityButtonClick);
            OnViewHidden -= RestoreSidebarPlacement;
        }

        private void RestoreSidebarPlacement() =>
            SetAnchor(null);

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
