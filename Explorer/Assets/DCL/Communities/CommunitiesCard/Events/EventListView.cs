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
        [field: SerializeField] private LoopListView2 loopList { get; set; }
        [field: SerializeField] private ScrollRect loopListScrollRect { get; set; }
        [field: SerializeField] private GameObject loadingObject { get; set; }
        [field: SerializeField] private GameObject emptyState { get; set; }
        [field: SerializeField] private GameObject emptyStateAdminText { get; set; }
        [field: SerializeField] private Button openWizardButton { get; set; }

        public event Action NewDataRequested;
        public event Action OpenWizardRequested;

        private Func<SectionFetchData<EventDTO>> getCurrentSectionFetchData;
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
            getCurrentSectionFetchData = currentSectionDataFunc;
        }

        private LoopListViewItem2 GetLoopListItemByIndex(LoopListView2 loopListView, int index)
        {
            throw new NotImplementedException();
        }

        public void RefreshGrid()
        {
            throw new NotImplementedException();
        }

        public void SetEmptyStateActive(bool active)
        {
            emptyState.SetActive(active);
            emptyStateAdminText.SetActive(canModify);
        }

        public void SetLoadingStateActive(bool active) =>
            loadingObject.SetActive(active);
    }
}
