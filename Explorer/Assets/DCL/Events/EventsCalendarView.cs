using DCL.EventsApi;
using DCL.UI.Utilities;
using DG.Tweening;
using SuperScrollView;
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.Events
{
    public class EventsCalendarView : MonoBehaviour
    {
        public event Action<DateTime>? DaySelectorButtonClicked;

        private const int EVENT_CARD_SMALL_PREFAB_INDEX = 0;
        private const int EVENT_CARD_BIG_PREFAB_INDEX = 1;
        private const int EVENT_CARD_EMPTY_PREFAB_INDEX = 2;

        public event Action<DateTime, int>? DaysRangeChanged;

        [Header("Days Selector")]
        [SerializeField] private GameObject daySelectorContainer = null!;
        [SerializeField] private List<EventsDaySelectorButton> daySelectorButtons = null!;
        [SerializeField] private Button previousDateRangeButton = null!;
        [SerializeField] private Button nextDateRangeButton = null!;

        [Header("Events")]
        [SerializeField] private GameObject loadingSpinner = null!;
        [SerializeField] private GameObject eventsContainer = null!;
        [SerializeField] private List<EventListConfiguration> eventsLists = null!;

        [Header("Highlighted Banner")]
        [SerializeField] private List<GameObject> objectsToHideWhenBanner = null!;
        [SerializeField] private EventCardView highlightedBanner = null!;

        [Serializable]
        private struct EventListConfiguration
        {
            public LoopListView2 eventsLoopList;
            public HoverableUiElement hoverableUiElement;
            public CanvasGroup scrollBarCanvasGroup;
        }

        private readonly Dictionary<int, List<string>> currentEventsIds = new ();
        private DateTime currentFromDate;
        private int currentNumberOfDaysShowed;
        private EventsStateService eventsStateService;

        private void Awake()
        {
            previousDateRangeButton.onClick.AddListener(() => SetupDaysSelector(currentFromDate.AddDays(-currentNumberOfDaysShowed), currentNumberOfDaysShowed));
            nextDateRangeButton.onClick.AddListener(() => SetupDaysSelector(currentFromDate.AddDays(currentNumberOfDaysShowed), currentNumberOfDaysShowed));

            foreach (EventsDaySelectorButton daySelectorButton in daySelectorButtons)
                daySelectorButton.ButtonClicked += OnDaySelectorButtonClicked;
        }

        private void OnDestroy()
        {
            previousDateRangeButton.onClick.RemoveAllListeners();
            nextDateRangeButton.onClick.RemoveAllListeners();

            foreach (EventsDaySelectorButton daySelectorButton in daySelectorButtons)
                daySelectorButton.ButtonClicked -= OnDaySelectorButtonClicked;
        }

        public void SetDependencies(EventsStateService eventsStateService) =>
            this.eventsStateService = eventsStateService;

        public void SetDaysSelectorActive(bool isActive) =>
            daySelectorContainer.SetActive(isActive);

        public void SetupDaysSelector(DateTime fromDate, int numberOfDaysToShow)
        {
            previousDateRangeButton.interactable = fromDate != DateTime.Today;

            for (var i = 0; i < daySelectorButtons.Count; i++)
            {
                EventsDaySelectorButton daySelectorButton = daySelectorButtons[i];
                daySelectorButton.Setup(fromDate.AddDays(i));
            }

            currentFromDate = fromDate;
            currentNumberOfDaysShowed = numberOfDaysToShow;
            DaysRangeChanged?.Invoke(fromDate, currentNumberOfDaysShowed);
        }

        public void SetHighlightedBanner(EventDTO? eventInfo)
        {
            foreach (GameObject go in objectsToHideWhenBanner)
                go.SetActive(eventInfo == null);

            highlightedBanner.gameObject.SetActive(eventInfo != null);

            if (eventInfo != null)
                highlightedBanner.Configure(eventInfo.Value);
        }

        public void InitializeEventsLists()
        {
            foreach (var eventList in eventsLists)
            {
                eventList.eventsLoopList.InitListView(0, SetupEventCardByIndex);
                eventList.eventsLoopList.gameObject.GetComponent<ScrollRect>()?.SetScrollSensitivityBasedOnPlatform();
                eventList.hoverableUiElement.HoverStateChanged += isHovering => eventList.scrollBarCanvasGroup.DOFade(isHovering ? 1f : 0f, 0.3f);
                eventList.scrollBarCanvasGroup.alpha = 0f;
            }
        }

        public void ClearAllEvents()
        {
            currentEventsIds.Clear();

            foreach (var eventList in eventsLists)
                eventList.eventsLoopList.SetListItemCount(0, false);
        }

        public void SetEvents(IReadOnlyList<EventDTO> events, int eventsListIndex, bool resetPos)
        {
            currentEventsIds.Remove(eventsListIndex);
            currentEventsIds.Add(eventsListIndex, new List<string>());
            foreach (EventDTO eventInfo in events)
                currentEventsIds[eventsListIndex].Add(eventInfo.id);

            eventsLists[eventsListIndex].eventsLoopList.SetListItemCount(currentEventsIds[eventsListIndex].Count, resetPos);
            eventsLists[eventsListIndex].eventsLoopList.ScrollRect.verticalNormalizedPosition = 1f;
        }

        public void SetAsLoading(bool isLoading)
        {
            loadingSpinner.SetActive(isLoading);
            eventsContainer.SetActive(!isLoading);
        }

        private LoopListViewItem2 SetupEventCardByIndex(LoopListView2 loopListView, int eventIndex)
        {
            int eventsListIndex = loopListView.transform.GetSiblingIndex();
            var eventInfo = eventsStateService.GetEventInfoById(currentEventsIds[eventsListIndex][eventIndex]);
            int itemPrefabIndex = eventInfo.live ? EVENT_CARD_BIG_PREFAB_INDEX : EVENT_CARD_SMALL_PREFAB_INDEX;
            LoopListViewItem2 listItem = loopListView.NewListViewItem(loopListView.ItemPrefabDataList[itemPrefabIndex].mItemPrefab.name);
            EventCardView cardView = listItem.GetComponent<EventCardView>();

            // Setup card data
            cardView.Configure(eventInfo);

            // Setup card events
            // ...

            return listItem;
        }

        private void OnDaySelectorButtonClicked(DateTime date) =>
            DaySelectorButtonClicked?.Invoke(date);
    }
}
