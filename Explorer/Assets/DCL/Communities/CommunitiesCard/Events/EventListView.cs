using DCL.EventsApi;
using DCL.UI.Utilities;
using SuperScrollView;
using System;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.Communities.CommunitiesCard.Events
{
    public class EventListView : MonoBehaviour, ICommunityFetchingView
    {
        private const int ELEMENT_MISSING_THRESHOLD = 5;

        [field: SerializeField] private LoopListView2 loopList { get; set; }
        [field: SerializeField] private ScrollRect loopListScrollRect { get; set; }
        [field: SerializeField] private GameObject loadingObject { get; set; }
        [field: SerializeField] private GameObject emptyState { get; set; }
        [field: SerializeField] private GameObject emptyStateAdminText { get; set; }
        [field: SerializeField] private Button openWizardButton { get; set; }

        public event Action NewDataRequested;
        public event Action OpenWizardRequested;

        private Func<SectionFetchData<EventDTO>> getEventsFetchData;
        private bool canModify;

        private void Awake()
        {
            loopListScrollRect.SetScrollSensitivityBasedOnPlatform();
            openWizardButton.onClick.AddListener(() => OpenWizardRequested?.Invoke());
        }

        public void SetCanModify(bool canModify)
        {
            this.canModify = canModify;
        }

        public void InitList(Func<SectionFetchData<EventDTO>> currentSectionDataFunc)
        {
            loopList.InitListView(0, GetLoopListItemByIndex);
            getEventsFetchData = currentSectionDataFunc;
        }

        private LoopListViewItem2 GetLoopListItemByIndex(LoopListView2 loopListView, int index)
        {
            SectionFetchData<EventDTO> eventData = getEventsFetchData();

            if (index >= eventData.totalFetched - ELEMENT_MISSING_THRESHOLD && eventData.totalFetched < eventData.totalToFetch)
                NewDataRequested?.Invoke();
            throw new NotImplementedException();
        }

        public void RefreshGrid()
        {
            loopList.SetListItemCount(getEventsFetchData().members.Count, false);
            loopList.RefreshAllShownItem();
        }

        public void SetEmptyStateActive(bool active)
        {
            emptyState.SetActive(active);
            emptyStateAdminText.SetActive(active && canModify);
        }

        public void SetLoadingStateActive(bool active)
        {
            loadingObject.SetActive(active);
            SetEmptyStateActive(!active);
        }
    }
}
