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
        [field: SerializeField] public ShopCategoryChipsView? CategoryChips { get; private set; }
        [field: SerializeField] public LoopGridView Grid { get; private set; } = null!;
        [field: SerializeField] public SkeletonLoadingView GridSkeleton { get; private set; } = null!;
        [field: SerializeField] public GameObject LoadingMoreSpinner { get; private set; } = null!;
        [field: SerializeField] public GameObject RefreshingSpinner { get; private set; } = null!;

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
        private int baseLeftPadding;
        private int baseRightPadding;

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

        private void OnEnable()
        {
            if (gridInitialized)
                CenterColumns();
        }

        private void OnRectTransformDimensionsChange()
        {
            if (gridInitialized && isActiveAndEnabled)
                CenterColumns();
        }

        public void InitializeGrid(Func<int, ShopItemCardModel?> modelAtIndex, Action<ShopItemCardView, ShopItemCardModel> bind)
        {
            if (gridInitialized)
                return;

            modelAt = modelAtIndex;
            bindCard = bind;
            baseLeftPadding = Grid.Padding.left;
            baseRightPadding = Grid.Padding.right;
            Grid.InitGridView(0, OnGetItem);
            Grid.ScrollRect.SetScrollSensitivityBasedOnPlatform();
            gridInitialized = true;
            ApplyColumns();
        }

        /// <summary>
        ///     The column count is decided by the layout around the grid (the item panel takes space away), not by the width.
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

        public void SetFirstLoadSkeleton(bool on)
        {
            if (on)
                GridSkeleton.ShowLoading();
            else
                GridSkeleton.HideLoading();
        }

        public void SetRefreshing(bool on)
        {
            RefreshingSpinner.SetActive(on);

            if (!on)
                return;

            Grid.gameObject.SetActive(false);
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
            CenterColumns();
        }

        private void CenterColumns()
        {
            // The grid viewport stretches across this anchor-driven rect, so its width is valid even before the layout pass runs
            float viewportWidth = ((RectTransform)transform).rect.width;

            if (viewportWidth <= 0f)
                return;

            float columnsWidth = (CurrentColumns * (Grid.ItemSize.x + Grid.ItemPadding.x)) - Grid.ItemPadding.x;
            int spare = Mathf.Max(0, Mathf.FloorToInt(viewportWidth - baseLeftPadding - baseRightPadding - columnsWidth));
            int left = baseLeftPadding + (spare / 2);
            int right = baseRightPadding + (spare - (spare / 2));
            RectOffset padding = Grid.Padding;

            if (padding.left == left && padding.right == right)
                return;

            Grid.SetPadding(new RectOffset(left, right, padding.top, padding.bottom));
        }
    }
}
