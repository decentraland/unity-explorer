using DCL.Communities;
using DCL.EventsApi;
using DCL.PlacesAPIService;
using DCL.UI;
using DCL.UI.Profiles.Helpers;
using DCL.UI.Utilities;
using DG.Tweening;
using SuperScrollView;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
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
        private const float SCROLL_STEP_PIXELS = 300f;
        private const float SCROLL_ANIMATION_DURATION = 0.3f;

        public event Action<DateTime, int>? DaysRangeChanged;

        [Header("Days Selector")]
        [SerializeField] private List<EventsDaySelectorButton> daySelectorButtons = null!;
        [SerializeField] private Button previousDateRangeButton = null!;
        [SerializeField] private Button nextDateRangeButton = null!;

        [Header("Events")]
        [SerializeField] private List<EventListConfiguration> eventsLists = null!;

        [Header("Highlighted Carousel")]
        [SerializeField] private List<GameObject> objectsToHideWhenBanner = null!;
        [SerializeField] private EventsHighlightedCarousel highlightedCarousel = null!;

        [Serializable]
        private struct EventListConfiguration
        {
            public LoopListView2 eventsLoopList;
            public HoverableUiElement hoverableUiElement;
            public CanvasGroup scrollBarCanvasGroup;
            public SkeletonLoadingView skeletonLoadingView;
            public Button moreEventsArrowUpButton;
            public Button moreEventsArrowDownButton;
            public ScrollRect scrollRect;
        }

        private readonly Dictionary<int, List<string>> currentEventsIds = new ();
        private readonly Dictionary<int, float> currentOccupancies = new (5);
        private readonly Dictionary<int, Tweener> activeScrollTweens = new ();
        private DateTime currentFromDate;
        private int currentNumberOfDaysShowed;
        private EventsStateService eventsStateService = null!;
        private ThumbnailLoader? eventCardsThumbnailLoader;
        private ProfileRepositoryWrapper? profileRepositoryWrapper;

        private void Awake()
        {
            previousDateRangeButton.onClick.AddListener(() => SetupDaysSelector(currentFromDate.AddDays(-currentNumberOfDaysShowed), currentNumberOfDaysShowed));
            nextDateRangeButton.onClick.AddListener(() => SetupDaysSelector(currentFromDate.AddDays(currentNumberOfDaysShowed), currentNumberOfDaysShowed));

            foreach (EventsDaySelectorButton daySelectorButton in daySelectorButtons)
                daySelectorButton.ButtonClicked += OnDaySelectorButtonClicked;

            for (var i = 0; i < eventsLists.Count; i++)
            {
                int eventsListIndex = i;
                EventListConfiguration eventListConfiguration = eventsLists[eventsListIndex];
                eventListConfiguration.scrollRect.onValueChanged.AddListener(_ => OnEventsScrollValueChanged(eventsListIndex));
                eventListConfiguration.moreEventsArrowUpButton.onClick.AddListener(() => ScrollEventsList(eventsListIndex, SCROLL_STEP_PIXELS));
                eventListConfiguration.moreEventsArrowDownButton.onClick.AddListener(() => ScrollEventsList(eventsListIndex, -SCROLL_STEP_PIXELS));
            }
        }

        private void OnDestroy()
        {
            previousDateRangeButton.onClick.RemoveAllListeners();
            nextDateRangeButton.onClick.RemoveAllListeners();

            foreach (EventsDaySelectorButton daySelectorButton in daySelectorButtons)
                daySelectorButton.ButtonClicked -= OnDaySelectorButtonClicked;

            foreach (EventListConfiguration eventListConfiguration in eventsLists)
            {
                eventListConfiguration.scrollRect.onValueChanged.RemoveAllListeners();
                eventListConfiguration.moreEventsArrowUpButton.onClick.RemoveAllListeners();
                eventListConfiguration.moreEventsArrowDownButton.onClick.RemoveAllListeners();
            }

            foreach (Tweener tween in activeScrollTweens.Values)
                tween?.Kill();

            activeScrollTweens.Clear();
        }

        public void SetDependencies(
            EventsStateService stateService,
            ThumbnailLoader thumbnailLoader,
            ProfileRepositoryWrapper profileRepoWrapper)
        {
            this.eventsStateService = stateService;
            this.eventCardsThumbnailLoader = thumbnailLoader;
            this.profileRepositoryWrapper = profileRepoWrapper;

            highlightedCarousel.SetDependencies(thumbnailLoader, profileRepoWrapper);
        }

        public void SetupDaysSelector(DateTime fromDate, int numberOfDaysToShow, bool triggerEvent = true, bool deactivateArrows = false)
        {
            bool isToday = fromDate.Date == DateTime.Today;
            nextDateRangeButton.interactable = !deactivateArrows;
            previousDateRangeButton.interactable = !deactivateArrows && !isToday;

            for (var i = 0; i < daySelectorButtons.Count; i++)
            {
                EventsDaySelectorButton daySelectorButton = daySelectorButtons[i];
                daySelectorButton.Setup(fromDate.AddDays(i));
            }

            currentFromDate = fromDate;
            currentNumberOfDaysShowed = numberOfDaysToShow;

            if (triggerEvent)
                DaysRangeChanged?.Invoke(fromDate, currentNumberOfDaysShowed);

            EventSystem.current.SetSelectedGameObject(null);
        }

        public void SetHighlightedCarousel(IReadOnlyList<EventDTO>? eventsInfo)
        {
            foreach (GameObject go in objectsToHideWhenBanner)
                go.SetActive(eventsInfo == null || eventsInfo.Count == 0);

            highlightedCarousel.gameObject.SetActive(eventsInfo is { Count: > 0 });

            if (eventsInfo != null)
            {
                List<EventsStateService.EventWithPlaceAndFriendsData> eventsData = new ();
                foreach (EventDTO eventInfo in eventsInfo)
                {
                    var eventData = eventsStateService.GetEventDataById(eventInfo.id);
                    if (eventData != null)
                        eventsData.Add(eventData);
                }

                // Setup card data
                highlightedCarousel.Configure(eventsData);

                // Setup card events
                highlightedCarousel.MainButtonClicked -= OnEventCardClicked;
                highlightedCarousel.MainButtonClicked += OnEventCardClicked;
                highlightedCarousel.InterestedButtonClicked -= OnEventInterestedButtonClicked;
                highlightedCarousel.InterestedButtonClicked += OnEventInterestedButtonClicked;
                highlightedCarousel.AddToCalendarButtonClicked -= OnEventAddToCalendarButtonClicked;
                highlightedCarousel.AddToCalendarButtonClicked += OnEventAddToCalendarButtonClicked;
                highlightedCarousel.JumpInButtonClicked -= OnEventJumpInButtonClicked;
                highlightedCarousel.JumpInButtonClicked += OnEventJumpInButtonClicked;
                highlightedCarousel.EventShareButtonClicked -= OnEventShareButtonClicked;
                highlightedCarousel.EventShareButtonClicked += OnEventShareButtonClicked;
                highlightedCarousel.EventCopyLinkButtonClicked -= OnEventCopyLinkButtonClicked;
                highlightedCarousel.EventCopyLinkButtonClicked += OnEventCopyLinkButtonClicked;
            }
        }

        public void ClearHighlightedEvents() =>
            highlightedCarousel.Clear();

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

            FillWithEmptyCards(events, eventsListIndex);

            eventsLists[eventsListIndex].eventsLoopList.SetListItemCount(currentEventsIds[eventsListIndex].Count, resetPos);
            eventsLists[eventsListIndex].eventsLoopList.ScrollRect.verticalNormalizedPosition = 1f;
        }

        public void SetAsLoading(bool isLoading)
        {
            foreach (var eventList in eventsLists)
            {
                if (isLoading)
                    eventList.skeletonLoadingView.ShowLoading();
                else
                    eventList.skeletonLoadingView.HideLoading();
            }
        }

        private LoopListViewItem2 SetupEventCardByIndex(LoopListView2 loopListView, int eventIndex)
        {
            int eventsListIndex = loopListView.transform.GetSiblingIndex();
            var eventData = eventsStateService.GetEventDataById(currentEventsIds[eventsListIndex][eventIndex]);
            int itemPrefabIndex = GetCardPrefabIndex(eventData);
            LoopListViewItem2 listItem = loopListView.NewListViewItem(loopListView.ItemPrefabDataList[itemPrefabIndex].mItemPrefab.name);
            EventCardView cardView = listItem.GetComponent<EventCardView>();

            // Setup card data
            if (eventData != null)
                cardView.Configure(eventData.EventInfo, eventCardsThumbnailLoader!, eventData.PlaceInfo, eventData.FriendsConnectedToPlace, profileRepositoryWrapper, eventData.CommunityInfo);

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

        private static int GetCardPrefabIndex(EventsStateService.EventWithPlaceAndFriendsData? eventData) =>
            eventData != null ?
                eventData.EventInfo.highlighted || eventData.EventInfo.live || eventData.EventInfo.attending || eventData.CommunityInfo != null ? EVENT_CARD_BIG_PREFAB_INDEX : EVENT_CARD_SMALL_PREFAB_INDEX :
                EVENT_CARD_EMPTY_PREFAB_INDEX;

        private void FillWithEmptyCards(IReadOnlyList<EventDTO> events, int eventsListIndex)
        {
            var columnOccupancyValue = 0f;

            foreach (EventDTO eventInfo in events)
            {
                var eventData = eventsStateService.GetEventDataById(eventInfo.id);
                int cardPrefabIndex = GetCardPrefabIndex(eventData);
                switch (cardPrefabIndex)
                {
                    case EVENT_CARD_BIG_PREFAB_INDEX:
                        columnOccupancyValue += 1f; break;
                    case EVENT_CARD_SMALL_PREFAB_INDEX:
                        columnOccupancyValue += 0.5f; break;
                }
            }

            currentOccupancies[eventsListIndex] = columnOccupancyValue;

            int numberOfEmptyCards = columnOccupancyValue switch
                                     {
                                         <= 0.5f => 3,
                                         <= 1.5f => 2,
                                         _ => 1,
                                     };

            for (var i = 0; i < numberOfEmptyCards; i++)
                currentEventsIds[eventsListIndex].Add(string.Empty);
        }

        private void OnEventsScrollValueChanged(int eventsListIndex)
        {
            bool scrollIsNotAtTheTop = eventsLists[eventsListIndex].scrollRect.verticalNormalizedPosition < 0.99f && currentOccupancies[eventsListIndex] > 1.5f;
            eventsLists[eventsListIndex].moreEventsArrowUpButton.gameObject.SetActive(scrollIsNotAtTheTop);

            bool scrollIsNotAtTheBottom = eventsLists[eventsListIndex].scrollRect.verticalNormalizedPosition > 0.01f && currentOccupancies[eventsListIndex] > 1.5f;
            eventsLists[eventsListIndex].moreEventsArrowDownButton.gameObject.SetActive(scrollIsNotAtTheBottom);
        }

        private void ScrollEventsList(int eventsListIndex, float pixelStep)
        {
            ScrollRect scrollRect = eventsLists[eventsListIndex].scrollRect;
            float contentHeight = scrollRect.content.rect.height;
            float viewportHeight = scrollRect.viewport.rect.height;
            float scrollableHeight = contentHeight - viewportHeight;

            if (scrollableHeight <= 0f)
                return;

            float normalizedStep = pixelStep / scrollableHeight;
            float targetPos = Mathf.Clamp01(scrollRect.verticalNormalizedPosition + normalizedStep);

            if (activeScrollTweens.TryGetValue(eventsListIndex, out Tweener existingTween))
                existingTween.Kill();

            activeScrollTweens[eventsListIndex] = scrollRect
                                                 .DOVerticalNormalizedPos(targetPos, SCROLL_ANIMATION_DURATION)
                                                 .SetEase(Ease.OutQuad);
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
