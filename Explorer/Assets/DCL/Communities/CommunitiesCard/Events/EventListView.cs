using DCL.UI.GenericContextMenu.Controls.Configs;
using DCL.UI.GenericContextMenuParameter;
using DCL.UI.Utilities;
using MVC;
using SuperScrollView;
using System;
using System.Threading;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.Communities.CommunitiesCard.Events
{
    public class EventListView : MonoBehaviour, ICommunityFetchingView<PlaceAndEventDTO>
    {
        private const int ELEMENT_MISSING_THRESHOLD = 5;

        [field: SerializeField] private LoopListView2 loopList { get; set; } = null!;
        [field: SerializeField] private ScrollRect loopListScrollRect { get; set; } = null!;
        [field: SerializeField] private GameObject loadingObject { get; set; } = null!;
        [field: SerializeField] private GameObject emptyState { get; set; } = null!;
        [field: SerializeField] private GameObject emptyStateAdminText { get; set; } = null!;
        [field: SerializeField] private Button openWizardButton { get; set; } = null!;
        [field: SerializeField] private CommunityEventsContextMenuConfiguration contextMenuConfiguration { get; set; } = null!;

        public event Action? NewDataRequested;
        public event Action? OpenWizardRequested;

        public event Action<PlaceAndEventDTO>? MainButtonClicked;
        public event Action<PlaceAndEventDTO>? JumpInButtonClicked;
        public event Action<PlaceAndEventDTO, EventListItemView>? InterestedButtonClicked;
        public event Action<PlaceAndEventDTO>? EventShareButtonClicked;
        public event Action<PlaceAndEventDTO>? EventCopyLinkButtonClicked;

        private SectionFetchData<PlaceAndEventDTO> eventsFetchData = null!;
        private bool canModify;
        private PlaceAndEventDTO lastClickedEventCtx;
        private CancellationToken cancellationToken;
        private GenericContextMenu? contextMenu;
        private ThumbnailLoader? thumbnailLoader;

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

        public void InitList(ThumbnailLoader newThumbnailLoader,
            CancellationToken panelCancellationToken)
        {
            loopList.InitListView(0, GetLoopListItemByIndex);
            cancellationToken = panelCancellationToken;
            this.thumbnailLoader = newThumbnailLoader;
        }

        private LoopListViewItem2 GetLoopListItemByIndex(LoopListView2 loopListView, int index)
        {
            SectionFetchData<PlaceAndEventDTO> eventData = eventsFetchData;

            LoopListViewItem2 item = loopList.NewListViewItem(loopList.ItemPrefabDataList[0].mItemPrefab.name);
            EventListItemView itemView = item.GetComponent<EventListItemView>();

            itemView.Configure(eventData.Items[index], thumbnailLoader!, cancellationToken);

            itemView.SubscribeToInteractions(data => MainButtonClicked?.Invoke(data),
                                             data => JumpInButtonClicked?.Invoke(data),
                                             data => InterestedButtonClicked?.Invoke(data, itemView),
                                             (data, position) => OpenCardContextMenu(data, position, itemView));

            if (index >= eventData.TotalFetched - ELEMENT_MISSING_THRESHOLD && eventData.TotalFetched < eventData.TotalToFetch)
                NewDataRequested?.Invoke();

            return item;
        }

        private void OpenCardContextMenu(PlaceAndEventDTO eventData, Vector2 position, EventListItemView eventListItemView)
        {
            lastClickedEventCtx = eventData;
            eventListItemView.CanPlayUnHoverAnimation = false;

            ViewDependencies.ContextMenuOpener.OpenContextMenu(new GenericContextMenuParameter(contextMenu, position,
                actionOnHide: () => eventListItemView.CanPlayUnHoverAnimation = true), cancellationToken);
        }

        public void RefreshGrid(SectionFetchData<PlaceAndEventDTO> data, bool redraw)
        {
            eventsFetchData = data;
            loopList.SetListItemCount(data.Items.Count, false);

            if (redraw)
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
