using DCL.UI;
using DCL.UI.Utilities;
using SuperScrollView;
using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Optional serialized references: Unity writes the backing field, so the private setter has no caller
// ReSharper disable UnusedAutoPropertyAccessor.Local
namespace DCL.Shop
{
    public class ShopGridSectionView : MonoBehaviour
    {
        public const int DEFAULT_COLUMNS = 6;
        public const int COMPACT_COLUMNS = 4;

        private const int LOAD_MORE_LOOKAHEAD_ROWS = 2;
        private const string EMPTY_SEARCH_FORMAT = "No items found for '{0}'.";
        private const string EMPTY_FILTERS_TEXT = "No items match these filters.";

        [field: Header("Grid")]
        [field: SerializeField] public ShopCategoryDropdownView? CategoryDropdown { get; private set; }
        [field: SerializeField] public LoopGridView Grid { get; private set; } = null!;
        [field: SerializeField] public SkeletonLoadingView GridSkeleton { get; private set; } = null!;
        [field: SerializeField] public GridLayoutGroup GhostGrid { get; private set; } = null!;
        [field: SerializeField] public GameObject LoadingMoreSpinner { get; private set; } = null!;

        [field: Header("States")]
        [field: SerializeField] public GameObject EmptyContainer { get; private set; } = null!;
        [field: SerializeField] public TMP_Text EmptyBody { get; private set; } = null!;
        [field: SerializeField] public Button BrowseButton { get; private set; } = null!;
        [field: SerializeField] public GameObject ErrorContainer { get; private set; } = null!;
        [field: SerializeField] public Button RetryButton { get; private set; } = null!;

        private Func<int, ShopItemCardModel?>? modelAt;
        private Action<ShopItemCardView, ShopItemCardModel>? bindCard;
        private int itemCount;
        private bool gridInitialized;

        public int CurrentColumns { get; private set; } = DEFAULT_COLUMNS;

        public event Action? GridNearEnd;
        public event Action? RetryClicked;
        public event Action? BrowseClicked;
        public event Action<ShopItemCardView>? CardAddToCartClicked;
        public event Action<ShopItemCardView>? CardBuyClicked;
        public event Action<ShopItemCardView>? CardClicked;

        private void Awake()
        {
            RetryButton.onClick.AddListener(() => RetryClicked?.Invoke());
            BrowseButton.onClick.AddListener(() => BrowseClicked?.Invoke());
        }

        public void InitializeGrid(Func<int, ShopItemCardModel?> modelAtIndex, Action<ShopItemCardView, ShopItemCardModel> bind)
        {
            if (gridInitialized)
                return;

            modelAt = modelAtIndex;
            bindCard = bind;
            Grid.InitGridView(0, OnGetItem);
            Grid.ScrollRect.SetScrollSensitivityBasedOnPlatform();
            gridInitialized = true;
            ApplyColumns();
        }

        /// <summary>
        ///     The column count is decided by the layout around the grid (the item panel takes space away), not by the width;
        ///     the columns keep the grid's left padding, so fewer columns never move the ones that stay.
        /// </summary>
        public void SetColumns(int columns)
        {
            if (columns == CurrentColumns)
                return;

            CurrentColumns = columns;

            if (gridInitialized)
                ApplyColumns();
        }

        public void SetItemCount(int count, bool resetPosition)
        {
            itemCount = count;
            Grid.SetListItemCount(count, resetPosition);

            if (resetPosition)
                Grid.ScrollRect.verticalNormalizedPosition = 1f;
        }

        public void RefreshAllShownItems() =>
            Grid.RefreshAllShownItem();

        public void RefreshItem(int index) =>
            Grid.RefreshItemByItemIndex(index);

        public void ForEachShownCard(Action<ShopItemCardView> action)
        {
            for (var i = 0; i < itemCount; i++)
            {
                LoopGridViewItem? item = Grid.GetShownItemByItemIndex(i);

                if (item != null && item.UserObjectData is ShopItemCardView card)
                    action(card);
            }
        }

        /// <summary>
        ///     Shows the card-sized ghost grid in place of the items while a first page loads; the skeleton view fades the grid itself.
        /// </summary>
        public void SetLoadingPage(bool on)
        {
            if (!on)
            {
                GridSkeleton.HideLoading();
                return;
            }

            GridSkeleton.ShowLoading();
            EmptyContainer.SetActive(false);
            ErrorContainer.SetActive(false);
        }

        public void SetLoadingMore(bool on) =>
            LoadingMoreSpinner.SetActive(on);

        public void SetEmpty(bool on, string? query)
        {
            EmptyContainer.SetActive(on);

            if (on)
                EmptyBody.text = query != null ? string.Format(EMPTY_SEARCH_FORMAT, query) : EMPTY_FILTERS_TEXT;
        }

        public void SetError(bool on) =>
            ErrorContainer.SetActive(on);

        public void SetGridVisible(bool visible) =>
            Grid.gameObject.SetActive(visible);

        private LoopGridViewItem? OnGetItem(LoopGridView grid, int index, int row, int column)
        {
            if (index < 0 || index >= itemCount || modelAt == null || bindCard == null)
                return null;

            ShopItemCardModel? model = modelAt(index);

            if (model == null)
                return null;

            LoopGridViewItem item = grid.NewListViewItem(grid.ItemPrefabDataList[0].mItemPrefab.name);

            if (item.UserObjectData is not ShopItemCardView card)
            {
                card = item.GetComponent<ShopItemCardView>();
                item.UserObjectData = card;
            }

            if (!item.IsInitHandlerCalled)
            {
                item.IsInitHandlerCalled = true;
                card.AddToCartClicked = c => CardAddToCartClicked?.Invoke(c);
                card.BuyClicked = c => CardBuyClicked?.Invoke(c);
                card.Clicked = c => CardClicked?.Invoke(c);
            }

            bindCard(card, model);

            if (index >= itemCount - (LOAD_MORE_LOOKAHEAD_ROWS * CurrentColumns))
                GridNearEnd?.Invoke();

            return item;
        }

        private void ApplyColumns()
        {
            Grid.SetGridFixedGroupCount(GridFixedType.ColumnCountFixed, CurrentColumns);
            GhostGrid.constraintCount = CurrentColumns;
        }
    }
}
