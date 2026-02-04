using DCL.Communities;
using DCL.EventsApi;
using DCL.PlacesAPIService;
using DCL.UI;
using DCL.UI.Utilities;
using SuperScrollView;
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.Events
{
    public class EventsByDayView : MonoBehaviour
    {
        public event Action? BackButtonClicked;
        public event Action? GoToNextDayButtonClicked;
        public event Action<EventDTO, PlacesData.PlaceInfo?, EventCardView>? EventCardClicked;

        [Header("Events Counter")]
        [SerializeField] private TMP_Text eventsCounter = null!;
        [SerializeField] private Button backButton = null!;

        [Header("Events Grid")]
        [SerializeField] private LoopGridView eventsLoopGrid = null!;
        [SerializeField] private GameObject emptyContainer = null!;
        [SerializeField] private SkeletonLoadingView skeletonLoading = null!;
        [SerializeField] private Button goToNextDayButton = null!;

        private EventsStateService eventsStateService = null!;
        private readonly List<string> currentEventsIds = new ();
        private ThumbnailLoader? eventCardsThumbnailLoader;

        private void Awake()
        {
            backButton.onClick.AddListener(() => BackButtonClicked?.Invoke());
            goToNextDayButton.onClick.AddListener(() => GoToNextDayButtonClicked?.Invoke());
        }

        private void OnDestroy()
        {
            backButton.onClick.RemoveAllListeners();
            goToNextDayButton.onClick.RemoveAllListeners();
        }

        public void SetDependencies(EventsStateService stateService, ThumbnailLoader thumbnailLoader)
        {
            this.eventsStateService = stateService;
            this.eventCardsThumbnailLoader = thumbnailLoader;
        }

        public void SetEventsCounter(string text) =>
            eventsCounter.text = text;

        public void InitializeEventsGrid()
        {
            eventsLoopGrid.InitGridView(0, SetupEventCardByIndex);
            eventsLoopGrid.gameObject.GetComponent<ScrollRect>()?.SetScrollSensitivityBasedOnPlatform();
        }

        public void SetEventsItems(IReadOnlyList<EventDTO> events, bool resetPos)
        {
            ClearEvents();

            foreach (EventDTO eventInfo in events)
                currentEventsIds.Add(eventInfo.id);

            eventsLoopGrid.SetListItemCount(currentEventsIds.Count, resetPos);

            SetEventsGridAsEmpty(currentEventsIds.Count == 0);

            if (resetPos)
                eventsLoopGrid.ScrollRect.verticalNormalizedPosition = 1f;
        }

        public void ClearEvents()
        {
            currentEventsIds.Clear();
            eventsLoopGrid.SetListItemCount(0, false);
            SetEventsGridAsEmpty(true);
        }

        public void SetEventsGridAsLoading(bool isLoading)
        {
            if (isLoading)
                skeletonLoading.ShowLoading();
            else
                skeletonLoading.HideLoading();
        }

        private LoopGridViewItem SetupEventCardByIndex(LoopGridView loopGridView, int index, int row, int column)
        {
            var eventData = eventsStateService.GetEventDataById(currentEventsIds[index]);
            LoopGridViewItem gridItem = loopGridView.NewListViewItem(loopGridView.ItemPrefabDataList[0].mItemPrefab.name);
            EventCardView cardView = gridItem.GetComponent<EventCardView>();

            // Setup card data
            if (eventData != null)
                cardView.Configure(eventData.EventInfo, eventCardsThumbnailLoader!, eventData.PlaceInfo);

            // Setup card events
            cardView.MainButtonClicked -= OnEventCardClicked;
            cardView.MainButtonClicked += OnEventCardClicked;

            return gridItem;
        }

        private void SetEventsGridAsEmpty(bool isEmpty)
        {
            emptyContainer.SetActive(isEmpty);
            eventsLoopGrid.gameObject.SetActive(!isEmpty);
        }

        private void OnEventCardClicked(EventDTO eventInfo, PlacesData.PlaceInfo? placeInfo, EventCardView eventCardView) =>
            EventCardClicked?.Invoke(eventInfo, placeInfo, eventCardView);
    }
}
