using DCL.UI.Utilities;
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
        [Header("Events")]
        [SerializeField] private List<EventsDaySelectorButton> daySelectorButtons = null!;

        [Header("Events")]
        [SerializeField] private GameObject loadingSpinner = null!;
        [SerializeField] private GameObject eventsContainer = null!;
        [SerializeField] private List<LoopListView2> eventsLoopLists = null!;

        private readonly Dictionary<int, List<string>> currentEventsIds = new ();

        private void Awake()
        {
            foreach (EventsDaySelectorButton daySelectorButton in daySelectorButtons)
                daySelectorButton.ButtonClicked += OnDaySelectorButtonClicked;
        }

        private void OnDestroy()
        {
            foreach (EventsDaySelectorButton daySelectorButton in daySelectorButtons)
                daySelectorButton.ButtonClicked -= OnDaySelectorButtonClicked;
        }

        public void SetupDaysSelector(DateTime initialDate)
        {
            for (var i = 0; i < daySelectorButtons.Count; i++)
            {
                EventsDaySelectorButton daySelectorButton = daySelectorButtons[i];
                daySelectorButton.Setup(initialDate.AddDays(i));
            }
        }

        public void InitializeEventsLists()
        {
            foreach (LoopListView2 eventList in eventsLoopLists)
            {
                eventList.InitListView(0, SetupEventCardByIndex);
                eventList.gameObject.GetComponent<ScrollRect>()?.SetScrollSensitivityBasedOnPlatform();
            }
        }

        public void ClearAllEvents()
        {
            currentEventsIds.Clear();

            foreach (LoopListView2 eventList in eventsLoopLists)
                eventList.SetListItemCount(0, false);
        }

        public void SetEvents(string[] events, int eventsListIndex, bool resetPos)
        {
            currentEventsIds.TryAdd(eventsListIndex, events.ToList());
            eventsLoopLists[eventsListIndex].SetListItemCount(currentEventsIds[eventsListIndex].Count, resetPos);
            eventsLoopLists[eventsListIndex].ScrollRect.verticalNormalizedPosition = 1f;
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
