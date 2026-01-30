using DCL.EventsApi;
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

        [Header("Events Counter")]
        [SerializeField] private TMP_Text eventsCounter = null!;
        [SerializeField] private Button backButton = null!;

        [Header("Events Grid")]
        [SerializeField] private LoopGridView eventsLoopGrid = null!;
        [SerializeField] private GameObject emptyContainer = null!;
        [SerializeField] private SkeletonLoadingView skeletonLoading = null!;

        private EventsStateService eventsStateService;
        private readonly List<string> currentEventsIds = new ();

        private void Awake() =>
            backButton.onClick.AddListener(() => BackButtonClicked?.Invoke());

        private void OnDestroy() =>
            backButton.onClick.RemoveAllListeners();

        public void SetDependencies(EventsStateService stateService) =>
            this.eventsStateService = stateService;

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
            var eventInfo = eventsStateService.GetEventInfoById(currentEventsIds[index]);
            LoopGridViewItem gridItem = loopGridView.NewListViewItem(loopGridView.ItemPrefabDataList[0].mItemPrefab.name);

            // Setup card data
            gridItem.GetComponentInChildren<TMP_Text>().text = $"{eventInfo.name}\n{DateTimeOffset.Parse(eventInfo.next_start_at).LocalDateTime.ToString("dd/MM/yyyy HH:mm")}"; // This is temporal until we implement the event card view.

            // Setup card events
            // ...

            return gridItem;
        }

        private void SetEventsGridAsEmpty(bool isEmpty)
        {
            emptyContainer.SetActive(isEmpty);
            eventsLoopGrid.gameObject.SetActive(!isEmpty);
        }
    }
}
