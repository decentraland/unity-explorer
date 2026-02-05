using DCL.Communities;
using DCL.EventsApi;
using DCL.PlacesAPIService;
using DCL.UI.Profiles.Helpers;
using DCL.UI.Utilities;
using DG.Tweening;
using SuperScrollView;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.Events
{
    public class EventsCalendarView : MonoBehaviour
    {
        public event Action<DateTime>? DaySelectorButtonClicked;
        public event Action<EventDTO, PlacesData.PlaceInfo?, EventCardView>? EventCardClicked;
        public event Action<EventDTO, EventCardView>? EventInterestedButtonClicked;
        public event Action<EventDTO>? EventAddToCalendarButtonClicked;
        public event Action<EventDTO>? EventJumpInButtonClicked;
        public event Action<EventDTO>? EventShareButtonClicked;
        public event Action<EventDTO>? EventCopyLinkButtonClicked;

        private const int EVENT_CARD_SMALL_PREFAB_INDEX = 0;
        private const int EVENT_CARD_BIG_PREFAB_INDEX = 1;
        private const int EVENT_CARD_EMPTY_PREFAB_INDEX = 2;

        public event Action<DateTime, int>? DaysRangeChanged;

        [Header("Days Selector")]
        [SerializeField] private GameObject daySelectorContainer = null!;
        [SerializeField] private List<EventsDaySelectorButton> daySelectorButtons = null!;
        [SerializeField] private Button previousDateRangeButton = null!;
        [SerializeField] private Button nextDateRangeButton = null!;
        [SerializeField] private Button goToTodayButtonLeftSide = null!;
        [SerializeField] private Button goToTodayButtonRightSide = null!;

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
        private EventsStateService eventsStateService = null!;
        private bool showGoToTodayButtonOnTheRight;
        private ThumbnailLoader? eventCardsThumbnailLoader;
        private ProfileRepositoryWrapper? profileRepositoryWrapper;

        private void Awake()
        {
            previousDateRangeButton.onClick.AddListener(() =>
            {
                showGoToTodayButtonOnTheRight = false;
                SetupDaysSelector(currentFromDate.AddDays(-currentNumberOfDaysShowed), currentNumberOfDaysShowed);
            });
            nextDateRangeButton.onClick.AddListener(() =>
            {
                showGoToTodayButtonOnTheRight = true;
                SetupDaysSelector(currentFromDate.AddDays(currentNumberOfDaysShowed), currentNumberOfDaysShowed);
            });

            DateTime todayAtTheBeginningOfTheDay = new DateTime(DateTime.Today.Year, DateTime.Today.Month, DateTime.Today.Day, 0, 0, 0, DateTimeKind.Local);
            goToTodayButtonLeftSide.onClick.AddListener(() => SetupDaysSelector(todayAtTheBeginningOfTheDay, currentNumberOfDaysShowed));
            goToTodayButtonRightSide.onClick.AddListener(() => SetupDaysSelector(todayAtTheBeginningOfTheDay, currentNumberOfDaysShowed));

            foreach (EventsDaySelectorButton daySelectorButton in daySelectorButtons)
                daySelectorButton.ButtonClicked += OnDaySelectorButtonClicked;
        }

        private void OnDestroy()
        {
            previousDateRangeButton.onClick.RemoveAllListeners();
            nextDateRangeButton.onClick.RemoveAllListeners();
            goToTodayButtonLeftSide.onClick.RemoveAllListeners();
            goToTodayButtonRightSide.onClick.RemoveAllListeners();

            foreach (EventsDaySelectorButton daySelectorButton in daySelectorButtons)
                daySelectorButton.ButtonClicked -= OnDaySelectorButtonClicked;
        }

        public void SetDependencies(
            EventsStateService stateService,
            ThumbnailLoader thumbnailLoader,
            ProfileRepositoryWrapper profileRepoWrapper)
        {
            this.eventsStateService = stateService;
            this.eventCardsThumbnailLoader = thumbnailLoader;
            this.profileRepositoryWrapper = profileRepoWrapper;
        }

        public void SetDaysSelectorActive(bool isActive) =>
            daySelectorContainer.SetActive(isActive);

        public void SetupDaysSelector(DateTime fromDate, int numberOfDaysToShow)
        {
            bool isToday = fromDate.Date == DateTime.Today;
            previousDateRangeButton.interactable = !isToday;
            goToTodayButtonLeftSide.gameObject.SetActive(!isToday && !showGoToTodayButtonOnTheRight);
            goToTodayButtonRightSide.gameObject.SetActive(!isToday && showGoToTodayButtonOnTheRight);

            for (var i = 0; i < daySelectorButtons.Count; i++)
            {
                EventsDaySelectorButton daySelectorButton = daySelectorButtons[i];
                daySelectorButton.Setup(fromDate.AddDays(i));
            }

            currentFromDate = fromDate;
            currentNumberOfDaysShowed = numberOfDaysToShow;
            DaysRangeChanged?.Invoke(fromDate, currentNumberOfDaysShowed);
        }

        public void SetHighlightedBanner(EventDTO? eventInfo, PlacesData.PlaceInfo? placeInfo)
        {
            foreach (GameObject go in objectsToHideWhenBanner)
                go.SetActive(eventInfo == null);

            highlightedBanner.gameObject.SetActive(eventInfo != null);

            if (eventInfo != null)
            {
                var eventData = eventsStateService.GetEventDataById(eventInfo.Value.id);

                // Setup card data
                if (eventData != null)
                    highlightedBanner.Configure(eventInfo.Value, eventCardsThumbnailLoader!, placeInfo, eventData.FriendsConnectedToPlace, profileRepositoryWrapper);

                // Setup card events
                highlightedBanner.MainButtonClicked -= OnEventCardClicked;
                highlightedBanner.MainButtonClicked += OnEventCardClicked;
                highlightedBanner.InterestedButtonClicked -= OnEventInterestedButtonClicked;
                highlightedBanner.InterestedButtonClicked += OnEventInterestedButtonClicked;
                highlightedBanner.AddToCalendarButtonClicked -= OnEventAddToCalendarButtonClicked;
                highlightedBanner.AddToCalendarButtonClicked += OnEventAddToCalendarButtonClicked;
                highlightedBanner.JumpInButtonClicked -= OnEventJumpInButtonClicked;
                highlightedBanner.JumpInButtonClicked += OnEventJumpInButtonClicked;
                highlightedBanner.EventShareButtonClicked -= OnEventShareButtonClicked;
                highlightedBanner.EventShareButtonClicked += OnEventShareButtonClicked;
                highlightedBanner.EventCopyLinkButtonClicked -= OnEventCopyLinkButtonClicked;
                highlightedBanner.EventCopyLinkButtonClicked += OnEventCopyLinkButtonClicked;
            }
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
            var eventData = eventsStateService.GetEventDataById(currentEventsIds[eventsListIndex][eventIndex]);
            int itemPrefabIndex = eventData != null ? EVENT_CARD_SMALL_PREFAB_INDEX : EVENT_CARD_EMPTY_PREFAB_INDEX;
            LoopListViewItem2 listItem = loopListView.NewListViewItem(loopListView.ItemPrefabDataList[itemPrefabIndex].mItemPrefab.name);
            EventCardView cardView = listItem.GetComponent<EventCardView>();

            // Setup card data
            if (eventData != null)
                cardView.Configure(eventData.EventInfo, eventCardsThumbnailLoader!, eventData.PlaceInfo, eventData.FriendsConnectedToPlace, profileRepositoryWrapper);

            // Setup card events
            cardView.MainButtonClicked -= OnEventCardClicked;
            cardView.MainButtonClicked += OnEventCardClicked;
            cardView.InterestedButtonClicked -= OnEventInterestedButtonClicked;
            cardView.InterestedButtonClicked += OnEventInterestedButtonClicked;
            cardView.AddToCalendarButtonClicked -= OnEventAddToCalendarButtonClicked;
            cardView.AddToCalendarButtonClicked += OnEventAddToCalendarButtonClicked;
            cardView.JumpInButtonClicked -= OnEventJumpInButtonClicked;
            cardView.JumpInButtonClicked += OnEventJumpInButtonClicked;
            cardView.EventShareButtonClicked -= OnEventShareButtonClicked;
            cardView.EventShareButtonClicked += OnEventShareButtonClicked;
            cardView.EventCopyLinkButtonClicked -= OnEventCopyLinkButtonClicked;
            cardView.EventCopyLinkButtonClicked += OnEventCopyLinkButtonClicked;

            return listItem;
        }

        private void OnDaySelectorButtonClicked(DateTime date) =>
            DaySelectorButtonClicked?.Invoke(date);

        private void OnEventCardClicked(EventDTO eventInfo, PlacesData.PlaceInfo? placeInfo, EventCardView eventCardView) =>
            EventCardClicked?.Invoke(eventInfo, placeInfo, eventCardView);

        private void OnEventInterestedButtonClicked(EventDTO eventInfo, EventCardView eventCardView) =>
            EventInterestedButtonClicked?.Invoke(eventInfo, eventCardView);

        private void OnEventAddToCalendarButtonClicked(EventDTO eventInfo) =>
            EventAddToCalendarButtonClicked?.Invoke(eventInfo);

        private void OnEventJumpInButtonClicked(EventDTO eventInfo) =>
            EventJumpInButtonClicked?.Invoke(eventInfo);

        private void OnEventShareButtonClicked(EventDTO eventInfo) =>
            EventShareButtonClicked?.Invoke(eventInfo);

        private void OnEventCopyLinkButtonClicked(EventDTO eventInfo) =>
            EventCopyLinkButtonClicked?.Invoke(eventInfo);
    }
}
