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
using PlaceInfo = DCL.PlacesAPIService.PlacesData.PlaceInfo;

namespace DCL.Communities.CommunitiesCard.Places
{
    public class PlacesSectionView : MonoBehaviour, ICommunityFetchingView
    {
        private const int ELEMENT_MISSING_THRESHOLD = 5;
        private const int ADD_PLACE_PREFAB_INDEX = 0;
        private const int PLACE_PREFAB_INDEX = 1;

        [field: SerializeField] private LoopGridView loopGrid { get; set; }
        [field: SerializeField] private ScrollRect loopGridScrollRect { get; set; }
        [field: SerializeField] private GameObject emptyState { get; set; }
        [field: SerializeField] private GameObject loadingObject { get; set; }
        [field: SerializeField] private CommunityPlaceContextMenuConfiguration contextMenuConfiguration { get; set; }

        public event Action? NewDataRequested;
        public event Action? AddPlaceRequested;

        public event Action<PlaceInfo, bool, PlaceCardView> ElementLikeToggleChanged;
        public event Action<PlaceInfo, bool, PlaceCardView> ElementDislikeToggleChanged;
        public event Action<PlaceInfo, bool, PlaceCardView> ElementFavoriteToggleChanged;
        public event Action<PlaceInfo> ElementShareButtonClicked;
        public event Action<PlaceInfo> ElementCopyLinkButtonClicked;
        public event Action<PlaceInfo> ElementInfoButtonClicked;
        public event Action<PlaceInfo> ElementJumpInButtonClicked;

        private Func<SectionFetchData<PlaceInfo>> getPlacesFetchData;
        private bool canModify;
        private IWebRequestController webRequestController;
        private IMVCManager mvcManager;
        private GenericContextMenu contextMenu;
        private CancellationToken cancellationToken;

        private PlaceInfo lastClickedPlaceCtx;

        private void Awake()
        {
            loopGridScrollRect.SetScrollSensitivityBasedOnPlatform();

            contextMenu = new GenericContextMenu(contextMenuConfiguration.ContextMenuWidth, verticalLayoutPadding: contextMenuConfiguration.VerticalPadding, elementsSpacing: contextMenuConfiguration.ElementsSpacing)
                         .AddControl(new ButtonContextMenuControlSettings(contextMenuConfiguration.ShareText, contextMenuConfiguration.ShareSprite, () => ElementShareButtonClicked?.Invoke(lastClickedPlaceCtx)))
                         .AddControl(new ButtonContextMenuControlSettings(contextMenuConfiguration.CopyLinkText, contextMenuConfiguration.CopyLinkSprite, () => ElementCopyLinkButtonClicked?.Invoke(lastClickedPlaceCtx)));
        }

        public void SetActive(bool active) => gameObject.SetActive(active);

        public void SetEmptyStateActive(bool active) =>
            emptyState.SetActive(active && !canModify);

        public void SetLoadingStateActive(bool active) =>
            loadingObject.SetActive(active);

        public void SetCanModify(bool canModify)
        {
            this.canModify = canModify;
        }

        public void InitGrid(Func<SectionFetchData<PlaceInfo>> placesDataFunc,
            IWebRequestController webRequestController,
            IMVCManager mvcManager,
            CancellationToken panelCancellationToken)
        {
            loopGrid.InitGridView(0, GetLoopGridItemByIndex);
            getPlacesFetchData = placesDataFunc;
            this.webRequestController = webRequestController;
            this.mvcManager = mvcManager;
            cancellationToken = panelCancellationToken;
        }

        private LoopGridViewItem GetLoopGridItemByIndex(LoopGridView loopGridView, int index, int row, int column)
        {
            if (canModify && index == 0)
            {
                LoopGridViewItem firstItem = loopGridView.NewListViewItem(loopGridView.ItemPrefabDataList[ADD_PLACE_PREFAB_INDEX].mItemPrefab.name);
                AddPlaceItemView addPlaceItemView = firstItem.GetComponent<AddPlaceItemView>();
                addPlaceItemView.SubscribeToInteraction(() => AddPlaceRequested?.Invoke());

                return firstItem;
            }

            LoopGridViewItem listItem = loopGridView.NewListViewItem(loopGridView.ItemPrefabDataList[PLACE_PREFAB_INDEX].mItemPrefab.name);
            PlaceCardView elementView = listItem.GetComponent<PlaceCardView>();

            SectionFetchData<PlaceInfo> membersData = getPlacesFetchData();

            int realIndex = canModify ? index - 1 : index;
            elementView.Configure(membersData.items[realIndex], webRequestController);

            elementView.SubscribeToInteractions((placeInfo, value, cardView) => ElementLikeToggleChanged?.Invoke(placeInfo, value, cardView),
                (placeInfo, value, cardView) => ElementDislikeToggleChanged?.Invoke(placeInfo, value, cardView),
                (placeInfo, value, cardView) => ElementFavoriteToggleChanged?.Invoke(placeInfo, value, cardView),
                OpenCardContextMenu,
                placeInfo => ElementInfoButtonClicked?.Invoke(placeInfo),
                placeInfo => ElementJumpInButtonClicked?.Invoke(placeInfo));

            if (realIndex >= membersData.totalFetched - ELEMENT_MISSING_THRESHOLD && membersData.totalFetched < membersData.totalToFetch)
                NewDataRequested?.Invoke();

            return listItem;
        }

        private void OpenCardContextMenu(PlaceInfo placeInfo, Vector2 position, PlaceCardView placeCardView)
        {
            lastClickedPlaceCtx = placeInfo;
            placeCardView.CanPlayUnHoverAnimation = false;

            mvcManager.ShowAndForget(GenericContextMenuController.IssueCommand(new GenericContextMenuParameter(contextMenu, position,
                actionOnHide: () => placeCardView.CanPlayUnHoverAnimation = true)), cancellationToken);
        }

        public void RefreshGrid()
        {
            int count = getPlacesFetchData().items.Count;

            //Account for the "Add Place" button if the user can modify the places
            if (canModify)
                count++;

            loopGrid.SetListItemCount(count, false);
            loopGrid.RefreshAllShownItem();
        }
    }
}
