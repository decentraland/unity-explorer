using Cysharp.Threading.Tasks;
using DCL.UI.GenericContextMenu;
using DCL.UI.GenericContextMenu.Controls.Configs;
using DCL.UI.Utilities;
using DCL.Web3.Identities;
using DCL.WebRequests;
using MVC;
using Nethereum.Siwe.Core.Recap;
using SuperScrollView;
using System;
using System.Threading;
using UnityEngine;
using UnityEngine.UI;
using PlaceInfo = DCL.PlacesAPIService.PlacesData.PlaceInfo;
using CommunityData = DCL.Communities.GetCommunityResponse.CommunityData;

namespace DCL.Communities.CommunitiesCard.Places
{
    public class PlacesSectionView : MonoBehaviour, ICommunityFetchingView
    {
        private const int ELEMENT_MISSING_THRESHOLD = 5;
        private const int ADD_PLACE_PREFAB_INDEX = 0;
        private const int PLACE_PREFAB_INDEX = 1;

        private const string DELETE_PLACE_TEXT_FORMAT = "Are you sure you want to delete [{0}] from [{1}]'s Places?";
        private const string DELETE_PLACE_SUB_TEXT = "Photos and events associated with this Place won't appear on the Community's profile anymore.";
        private const string DELETE_PLACE_CANCEL_TEXT = "CANCEL";
        private const string DELETE_PLACE_CONFIRM_TEXT = "DELETE";

        [field: SerializeField] private LoopGridView loopGrid { get; set; }
        [field: SerializeField] private ScrollRect loopGridScrollRect { get; set; }
        [field: SerializeField] private GameObject emptyState { get; set; }
        [field: SerializeField] private GameObject loadingObject { get; set; }
        [field: SerializeField] private CommunityPlaceContextMenuConfiguration contextMenuConfiguration { get; set; }
        [field: SerializeField] private ConfirmationDialogView confirmationDialogView { get; set; }
        [field: SerializeField] private Sprite deleteSprite { get; set; }

        public event Action? NewDataRequested;
        public event Action? AddPlaceRequested;

        public event Action<PlaceInfo, bool, PlaceCardView> ElementLikeToggleChanged;
        public event Action<PlaceInfo, bool, PlaceCardView> ElementDislikeToggleChanged;
        public event Action<PlaceInfo, bool, PlaceCardView> ElementFavoriteToggleChanged;
        public event Action<PlaceInfo> ElementShareButtonClicked;
        public event Action<PlaceInfo> ElementCopyLinkButtonClicked;
        public event Action<PlaceInfo> ElementInfoButtonClicked;
        public event Action<PlaceInfo> ElementJumpInButtonClicked;
        public event Action<PlaceInfo> ElementDeleteButtonClicked;

        private Func<SectionFetchData<PlaceInfo>> getPlacesFetchData;
        private bool canModify;
        private CommunityData communityData;
        private IWebRequestController webRequestController;
        private IMVCManager mvcManager;
        private GenericContextMenu contextMenu;
        private CancellationToken cancellationToken;
        private IWeb3IdentityCache web3IdentityCache;

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

        public void SetCommunityData(CommunityData community)
        {
            communityData = community;
        }

        public void InitGrid(Func<SectionFetchData<PlaceInfo>> placesDataFunc,
            IWebRequestController webRequestController,
            IMVCManager mvcManager,
            CancellationToken panelCancellationToken,
            IWeb3IdentityCache web3IdentityCache)
        {
            loopGrid.InitGridView(0, GetLoopGridItemByIndex);
            getPlacesFetchData = placesDataFunc;
            this.webRequestController = webRequestController;
            this.mvcManager = mvcManager;
            cancellationToken = panelCancellationToken;
            this.web3IdentityCache = web3IdentityCache;
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
            PlaceInfo placeInfo = membersData.items[realIndex];
            elementView.Configure(placeInfo, placeInfo.owner.EqualsIgnoreCase(web3IdentityCache.Identity?.Address) && canModify, webRequestController);

            elementView.SubscribeToInteractions((placeInfo, value, cardView) => ElementLikeToggleChanged?.Invoke(placeInfo, value, cardView),
                (placeInfo, value, cardView) => ElementDislikeToggleChanged?.Invoke(placeInfo, value, cardView),
                (placeInfo, value, cardView) => ElementFavoriteToggleChanged?.Invoke(placeInfo, value, cardView),
                OpenCardContextMenu,
                placeInfo => ElementInfoButtonClicked?.Invoke(placeInfo),
                placeInfo => ElementJumpInButtonClicked?.Invoke(placeInfo),
                placeInfo => ShowBanConfirmationDialog(placeInfo, communityData.name));

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

        private void ShowBanConfirmationDialog(PlaceInfo placeInfo, string communityName)
        {
            ShowBanConfirmationDialogAsync(cancellationToken).Forget();
            return;

            async UniTaskVoid ShowBanConfirmationDialogAsync(CancellationToken ct)
            {
                ConfirmationDialogView.ConfirmationResult dialogResult = await confirmationDialogView.ShowConfirmationDialogAsync(
                    new ConfirmationDialogView.DialogData(string.Format(DELETE_PLACE_TEXT_FORMAT, placeInfo.title, communityName),
                        DELETE_PLACE_CANCEL_TEXT,
                        DELETE_PLACE_CONFIRM_TEXT,
                        deleteSprite,
                        false, false,
                        DELETE_PLACE_SUB_TEXT),
                    ct);

                if (dialogResult == ConfirmationDialogView.ConfirmationResult.CANCEL) return;

                ElementDeleteButtonClicked?.Invoke(placeInfo);
            }
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
