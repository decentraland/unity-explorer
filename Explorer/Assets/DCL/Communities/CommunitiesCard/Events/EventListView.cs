using DCL.EventsApi;
using DCL.UI.GenericContextMenu;
using DCL.UI.GenericContextMenu.Controls.Configs;
using DCL.UI.Utilities;
using DCL.WebRequests;
using MVC;
using SuperScrollView;
using System;
using System.Threading;
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
        [field: SerializeField] private CommunityEventsContextMenuConfiguration contextMenuConfiguration { get; set; }

        public event Action NewDataRequested;
        public event Action OpenWizardRequested;

        public event Action<EventDTO> MainButtonClicked;
        public event Action<EventDTO> JumpInButtonClicked;
        public event Action<EventDTO> InterestedButtonClicked;
        public event Action<EventDTO> EventShareButtonClicked;
        public event Action<EventDTO> EventCopyLinkButtonClicked;

        private Func<SectionFetchData<EventDTO>> getEventsFetchData;
        private bool canModify;
        private IWebRequestController webRequestController;
        private IMVCManager mvcManager;
        private EventDTO lastClickedEventCtx;
        private CancellationToken cancellationToken;
        private GenericContextMenu contextMenu;

        private void Awake()
        {
            loopListScrollRect.SetScrollSensitivityBasedOnPlatform();
            openWizardButton.onClick.AddListener(() => OpenWizardRequested?.Invoke());

            contextMenu = new GenericContextMenu(contextMenuConfiguration.ContextMenuWidth, verticalLayoutPadding: contextMenuConfiguration.VerticalPadding, elementsSpacing: contextMenuConfiguration.ElementsSpacing)
                         .AddControl(new ButtonContextMenuControlSettings(contextMenuConfiguration.ShareText, contextMenuConfiguration.ShareSprite, () => EventShareButtonClicked?.Invoke(lastClickedEventCtx)))
                         .AddControl(new ButtonContextMenuControlSettings(contextMenuConfiguration.CopyLinkText, contextMenuConfiguration.CopyLinkSprite, () => EventCopyLinkButtonClicked?.Invoke(lastClickedEventCtx)));
        }

        public void SetCanModify(bool canModify)
        {
            this.canModify = canModify;
        }

        public void InitList(Func<SectionFetchData<EventDTO>> currentSectionDataFunc,
            IWebRequestController webRequestController,
            IMVCManager mvcManager,
            CancellationToken panelCancellationToken)
        {
            loopList.InitListView(0, GetLoopListItemByIndex);
            getEventsFetchData = currentSectionDataFunc;
            this.webRequestController = webRequestController;
            this.mvcManager = mvcManager;
            cancellationToken = panelCancellationToken;
        }

        private LoopListViewItem2 GetLoopListItemByIndex(LoopListView2 loopListView, int index)
        {
            SectionFetchData<EventDTO> eventData = getEventsFetchData();

            LoopListViewItem2 item = loopList.NewListViewItem(loopList.ItemPrefabDataList[0].mItemPrefab.name);
            EventListItemView itemView = item.GetComponent<EventListItemView>();

            itemView.Configure(eventData.members[index], webRequestController);

            itemView.SubscribeToInteractions(data => MainButtonClicked?.Invoke(data),
                                             data => JumpInButtonClicked?.Invoke(data),
                                             data => InterestedButtonClicked?.Invoke(data),
                                             OpenCardContextMenu);

            if (index >= eventData.totalFetched - ELEMENT_MISSING_THRESHOLD && eventData.totalFetched < eventData.totalToFetch)
                NewDataRequested?.Invoke();

            return item;
        }

        private void OpenCardContextMenu(EventDTO eventData, Vector2 position, EventListItemView eventListItemView)
        {
            lastClickedEventCtx = eventData;
            eventListItemView.CanUnHover = false;

            mvcManager.ShowAndForget(GenericContextMenuController.IssueCommand(new GenericContextMenuParameter(contextMenu, position,
                actionOnHide: () => eventListItemView.CanUnHover = true)), cancellationToken);
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
