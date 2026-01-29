using DCL.UI.Utilities;
using DG.Tweening;
using SuperScrollView;
using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.Events
{
    public class EventsCalendarView : MonoBehaviour
    {
        public event Action<DateTime, int>? DaysRangeChanged;

        [Header("Days Selector")]
        [SerializeField] private List<EventsDaySelectorButton> daySelectorButtons = null!;
        [SerializeField] private Button previousDateRangeButton = null!;
        [SerializeField] private Button nextDateRangeButton = null!;

        [Header("Events")]
        [SerializeField] private GameObject loadingSpinner = null!;
        [SerializeField] private GameObject eventsContainer = null!;
        [SerializeField] private List<EventListConfiguration> eventsLists = null!;

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

        public void SetupDaysSelector(DateTime fromDate, int numberOfDaysToShow)
        {
            for (var i = 0; i < daySelectorButtons.Count; i++)
            {
                EventsDaySelectorButton daySelectorButton = daySelectorButtons[i];
                daySelectorButton.Setup(fromDate.AddDays(i));
            }

            currentFromDate = fromDate;
            currentNumberOfDaysShowed = numberOfDaysToShow;
            DaysRangeChanged?.Invoke(fromDate, currentNumberOfDaysShowed);
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

        public void SetEvents(string[] events, int eventsListIndex, bool resetPos)
        {
            currentEventsIds.TryAdd(eventsListIndex, events.ToList());
            eventsLists[eventsListIndex].eventsLoopList.SetListItemCount(currentEventsIds[eventsListIndex].Count, resetPos);
            eventsLists[eventsListIndex].eventsLoopList.ScrollRect.verticalNormalizedPosition = 1f;
        }

        public void SetAsLoading(bool isLoading)
        {
            loadingSpinner.SetActive(isLoading);
            eventsContainer.SetActive(!isLoading);
        }

        private LoopListViewItem2 SetupEventCardByIndex(LoopListView2 loopListView, int index)
        {
            int eventsListIndex = loopListView.transform.GetSiblingIndex();
            string eventInfo = currentEventsIds[eventsListIndex][index];
            LoopListViewItem2 listItem = loopListView.NewListViewItem(loopListView.ItemPrefabDataList[0].mItemPrefab.name);
            listItem.GetComponentInChildren<TMP_Text>().text = eventInfo;

            // Setup card data
            // ...

            // Setup card events
            // ...

            return listItem;
        }

        private void OnDaySelectorButtonClicked(DateTime date)
        {

        }
    }
}
