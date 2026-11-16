using DCL.UI;
using DCL.UI.Utilities;
using SuperScrollView;
using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.Shop
{
    /// <summary>
    ///     A paged, virtualized grid of shop cards with its loading, empty and error states; the card and what it shows are the subclass's.
    /// </summary>
    public abstract class ShopGridViewBase<TCard, TModel> : MonoBehaviour where TCard: MonoBehaviour where TModel: class
    {
        public const int DEFAULT_COLUMNS = 6;
        public const int COMPACT_COLUMNS = 4;

        private const int LOAD_MORE_LOOKAHEAD_ROWS = 2;

        [field: Header("Grid")]
        [field: SerializeField] public LoopGridView Grid { get; private set; } = null!;
        [field: SerializeField] public SkeletonLoadingView GridSkeleton { get; private set; } = null!;
        [field: SerializeField] public GridLayoutGroup GhostGrid { get; private set; } = null!;
        [field: SerializeField] public GameObject LoadingMoreSpinner { get; private set; } = null!;

        [field: Header("States")]
        [field: SerializeField] public GameObject EmptyContainer { get; private set; } = null!;
        [field: SerializeField] public TMP_Text EmptyBody { get; private set; } = null!;
        [field: SerializeField] public GameObject ErrorContainer { get; private set; } = null!;
        [field: SerializeField] public Button RetryButton { get; private set; } = null!;

        private Func<int, TModel?>? modelAt;
        private Action<TCard, TModel>? bindCard;
        private int itemCount;
        private bool gridInitialized;

        public int CurrentColumns { get; private set; } = DEFAULT_COLUMNS;

        public event Action? GridNearEnd;
        public event Action? RetryClicked;

        [JetBrains.Annotations.UsedImplicitly] // Unity event function
        protected virtual void Awake()
        {
            RetryButton.onClick.AddListener(() => RetryClicked?.Invoke());
        }

        public void InitializeGrid(Func<int, TModel?> modelAtIndex, Action<TCard, TModel> bind)
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

        public void ForEachShownCard(Action<TCard> action)
        {
            for (var i = 0; i < itemCount; i++)
            {
                LoopGridViewItem? item = Grid.GetShownItemByItemIndex(i);

                if (item != null && item.UserObjectData is TCard card)
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

        public void ShowEmpty(bool on, string message)
        {
            EmptyContainer.SetActive(on);

            if (on)
                EmptyBody.text = message;
        }

        public void SetError(bool on) =>
            ErrorContainer.SetActive(on);

        public void SetGridVisible(bool visible) =>
            Grid.gameObject.SetActive(visible);

        /// <summary>
        ///     Hooks the callbacks of a card the pool has just created; a pooled card is wired once and rebound many times.
        /// </summary>
        protected abstract void WireCard(TCard card);

        private LoopGridViewItem? OnGetItem(LoopGridView grid, int index, int row, int column)
        {
            if (index < 0 || index >= itemCount || modelAt == null || bindCard == null)
                return null;

            TModel? model = modelAt(index);

            if (model == null)
                return null;

            LoopGridViewItem item = grid.NewListViewItem(grid.ItemPrefabDataList[0].mItemPrefab.name);

            if (item.UserObjectData is not TCard card)
            {
                card = item.GetComponent<TCard>();
                item.UserObjectData = card;
            }

            if (!item.IsInitHandlerCalled)
            {
                item.IsInitHandlerCalled = true;
                WireCard(card);
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
