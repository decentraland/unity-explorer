using Cysharp.Threading.Tasks;
using DCL.Communities;
using DCL.EventsApi;
using DCL.PlacesAPIService;
using DCL.UI.Profiles.Helpers;
using DG.Tweening;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.Pool;
using UnityEngine.UI;
using Utility;

namespace DCL.Events
{
    public class EventsHighlightedCarousel : MonoBehaviour
    {
        private const int DOT_BUTTONS_POOL_DEFAULT_CAPACITY = 5;
        private const float DOTS_ANIMATION_DURATION = 0.3f;
        private const int CAROUSEL_DELAY_MS = 10000;

        public event Action<EventDTO, PlacesData.PlaceInfo?, EventCardView>? MainButtonClicked;
        public event Action<EventDTO, EventCardView>? InterestedButtonClicked;
        public event Action<EventDTO>? AddToCalendarButtonClicked;
        public event Action<EventDTO>? JumpInButtonClicked;
        public event Action<EventDTO>? EventShareButtonClicked;
        public event Action<EventDTO>? EventCopyLinkButtonClicked;

        [Header("Events Container Configuration")]
        [SerializeField] private CanvasGroup mainCanvasGroup = null!;
        [SerializeField] private EventCardBannerView eventCard = null!;

        [Header("Dots Configuration")]
        [SerializeField] private Transform dotsSelectorContainer = null!;
        [SerializeField] private Button dotButtonPrefab = null!;
        [SerializeField] private Color selectedDotColor;
        [SerializeField] private float selectedDotWidth = 40f;
        [SerializeField] private Color nonSelectedDotColor;
        [SerializeField] private float nonselectedDotWidth = 14f;

        private ThumbnailLoader? eventCardsThumbnailLoader;
        private ProfileRepositoryWrapper? profileRepositoryWrapper;
        private IObjectPool<Button> dotButtonsPool = null!;
        private IReadOnlyList<EventsStateService.EventWithPlaceAndFriendsData> currentEvents = null!;
        private readonly List<Button> currentDotButtons = new ();
        private CancellationTokenSource? carouselCts;
        private int currentCarouselIndex;

        private void Awake()
        {
            // Dots pool configuration
            dotButtonsPool = new ObjectPool<Button>(
                InstantiateDotButtonGameObject,
                defaultCapacity: DOT_BUTTONS_POOL_DEFAULT_CAPACITY,
                actionOnGet: dotButton =>
                {
                    dotButton.gameObject.SetActive(true);
                    dotButton.transform.SetAsLastSibling();
                },
                actionOnRelease: dotButtonView => dotButtonView.gameObject.SetActive(false));

            eventCard.MainButtonClicked += OnEventCardClicked;
            eventCard.InterestedButtonClicked += OnEventInterestedButtonClicked;
            eventCard.AddToCalendarButtonClicked += OnEventAddToCalendarButtonClicked;
            eventCard.JumpInButtonClicked += OnEventJumpInButtonClicked;
            eventCard.EventShareButtonClicked += OnEventShareButtonClicked;
            eventCard.EventCopyLinkButtonClicked += OnEventCopyLinkButtonClicked;
        }

        private void OnDestroy()
        {
            eventCard.MainButtonClicked -= OnEventCardClicked;
            eventCard.InterestedButtonClicked -= OnEventInterestedButtonClicked;
            eventCard.AddToCalendarButtonClicked -= OnEventAddToCalendarButtonClicked;
            eventCard.JumpInButtonClicked -= OnEventJumpInButtonClicked;
            eventCard.EventShareButtonClicked -= OnEventShareButtonClicked;
            eventCard.EventCopyLinkButtonClicked -= OnEventCopyLinkButtonClicked;
        }

        public void SetDependencies(ThumbnailLoader thumbnailLoader, ProfileRepositoryWrapper profileRepoWrapper)
        {
            this.eventCardsThumbnailLoader = thumbnailLoader;
            this.profileRepositoryWrapper = profileRepoWrapper;
        }

        public void Configure(IReadOnlyList<EventsStateService.EventWithPlaceAndFriendsData> eventsData)
        {
            Clear();

            currentEvents = eventsData;

            if (currentEvents.Count > 1)
            {
                // Configure dots selector
                for (var i = 0; i < currentEvents.Count; i++)
                {
                    var dotButton = dotButtonsPool.Get();
                    dotButton.onClick.RemoveAllListeners();
                    dotButton.onClick.AddListener(() => SelectEvent(dotButton.transform.GetSiblingIndex()));
                    currentDotButtons.Add(dotButton);
                }
            }

            SelectEvent(0);
        }

        public void Clear()
        {
            carouselCts.SafeCancelAndDispose();

            foreach (var dotButton in currentDotButtons)
                dotButtonsPool.Release(dotButton);

            currentDotButtons.Clear();
        }

        private Button InstantiateDotButtonGameObject()
        {
            Button dotButton = Instantiate(dotButtonPrefab, dotsSelectorContainer);
            return dotButton;
        }

        private void SelectEvent(int eventIndex)
        {
            for (var i = 0; i < currentDotButtons.Count; i++)
            {
                Button dotButton = currentDotButtons[i];
                AnimateDot(dotButton, i == eventIndex);
            }

            if (eventIndex >= currentEvents.Count)
                return;

            var selectedEvent = currentEvents[eventIndex];
            eventCard.Configure(selectedEvent.EventInfo, eventCardsThumbnailLoader!, selectedEvent.PlaceInfo, selectedEvent.FriendsConnectedToPlace, profileRepositoryWrapper, selectedEvent.CommunityInfo);

            currentCarouselIndex = eventIndex;

            if (currentEvents.Count > 1)
            {
                carouselCts = carouselCts.SafeRestart();
                PlayCarouselAsync(carouselCts.Token).Forget();
            }
        }

        private void AnimateDot(Button dotButton, bool isSelected)
        {
            dotButton.image.color = isSelected ? selectedDotColor : nonSelectedDotColor;

            RectTransform rt = (RectTransform)dotButton.transform;
            DOTween.To(
                () => rt.rect.width,
                x => rt.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, x),
                isSelected ? selectedDotWidth : nonselectedDotWidth,
                DOTS_ANIMATION_DURATION
            ).SetEase(Ease.OutCubic);
        }

        private async UniTask PlayCarouselAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                await UniTask.Delay(CAROUSEL_DELAY_MS, cancellationToken: ct);
                int nextEventIndex = (currentCarouselIndex + 1) % currentEvents.Count;
                SelectEvent(nextEventIndex);
            }
        }

        private void OnEventCardClicked(EventDTO eventInfo, PlacesData.PlaceInfo? placeInfo, EventCardView eventCardView) =>
            MainButtonClicked?.Invoke(eventInfo, placeInfo, eventCardView);

        private void OnEventInterestedButtonClicked(EventDTO eventInfo, EventCardView eventCardView) =>
            InterestedButtonClicked?.Invoke(eventInfo, eventCardView);

        private void OnEventAddToCalendarButtonClicked(EventDTO eventInfo) =>
            AddToCalendarButtonClicked?.Invoke(eventInfo);

        private void OnEventJumpInButtonClicked(EventDTO eventInfo) =>
            JumpInButtonClicked?.Invoke(eventInfo);

        private void OnEventShareButtonClicked(EventDTO eventInfo) =>
            EventShareButtonClicked?.Invoke(eventInfo);

        private void OnEventCopyLinkButtonClicked(EventDTO eventInfo) =>
            EventCopyLinkButtonClicked?.Invoke(eventInfo);
    }
}
