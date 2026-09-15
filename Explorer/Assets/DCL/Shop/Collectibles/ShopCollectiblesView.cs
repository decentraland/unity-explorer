using Cysharp.Threading.Tasks;
using DCL.UI;
using DCL.UI.Utilities;
using SuperScrollView;
using System;
using System.Collections.Generic;
using System.Threading;
using TMPro;
using UnityEngine;
using UnityEngine.Pool;
using UnityEngine.UI;
using Utility;

namespace DCL.Shop
{
    public class ShopCollectiblesView : MonoBehaviour
    {
        private const int MIN_COLUMNS = 2;
        private const int MAX_COLUMNS = 6;
        private const int LOAD_MORE_LOOKAHEAD_ROWS = 2;
        private const int SEARCH_DEBOUNCE_MS = 500;
        private const int CHIPS_POOL_CAPACITY = 12;
        private const string COUNTER_FORMAT = "{0} items";
        private const string COUNTER_SEARCH_FORMAT = "{0} items for '{1}'";
        private const string EMPTY_SEARCH_FORMAT = "No items found for '{0}'.";
        private const string EMPTY_FILTERS_TEXT = "No items match these filters.";

        [field: Header("Toolbar")]
        [field: SerializeField] public SearchBarView SearchBar { get; private set; } = null!;
        [field: SerializeField] public TMP_Text ResultCounter { get; private set; } = null!;
        [field: SerializeField] public GameObject ResultCounterSkeleton { get; private set; } = null!;
        [field: SerializeField] public DropdownView SortDropdown { get; private set; } = null!;
        [field: SerializeField] public GameObject ChipsRow { get; private set; } = null!;
        [field: SerializeField] public Transform ChipsContainer { get; private set; } = null!;
        [field: SerializeField] public ShopFilterChipView ChipPrefab { get; private set; } = null!;
        [field: SerializeField] public Button ClearAllButton { get; private set; } = null!;

        [field: Header("Grid")]
        [field: SerializeField] public LoopGridView Grid { get; private set; } = null!;
        [field: SerializeField] public SkeletonLoadingView GridSkeleton { get; private set; } = null!;
        [field: SerializeField] public GameObject LoadingMoreSpinner { get; private set; } = null!;
        [field: SerializeField] public GameObject RefreshingSpinner { get; private set; } = null!;

        [field: Header("States")]
        [field: SerializeField] public GameObject EmptyContainer { get; private set; } = null!;
        [field: SerializeField] public TMP_Text EmptyBody { get; private set; } = null!;
        [field: SerializeField] public Button BackToOverviewButton { get; private set; } = null!;
        [field: SerializeField] public GameObject ErrorContainer { get; private set; } = null!;
        [field: SerializeField] public Button RetryButton { get; private set; } = null!;

        private readonly List<ShopFilterChipView> activeChips = new ();
        private IObjectPool<ShopFilterChipView>? chipsPool;
        private Func<int, ShopItemCardModel?>? modelAt;
        private Action<ShopItemCardView, ShopItemCardModel>? bindCard;
        private CancellationTokenSource? searchCts;
        private int itemCount;
        private bool gridInitialized;

        public bool IsSearchBarFocused => SearchBar.inputField.isFocused;

        public int CurrentColumns { get; private set; } = 5;

        public event Action? SearchBarSelected;
        public event Action? SearchBarDeselected;
        public event Action<string>? SearchChanged;
        public event Action<ShopSortOption>? SortChanged;
        public event Action<ShopFilterChipView>? ChipRemoveClicked;
        public event Action? ClearAllClicked;
        public event Action? GridNearEnd;
        public event Action? RetryClicked;
        public event Action? BackToOverviewClicked;
        public event Action<ShopItemCardView>? CardAddToCartClicked;
        public event Action<ShopItemCardView>? CardBuyClicked;
        public event Action<ShopItemCardView>? CardViewClicked;

        private void Awake()
        {
            EnsureChipsPool();
            SearchBar.inputField.onValueChanged.AddListener(OnSearchValueChanged);
            SearchBar.inputField.onSubmit.AddListener(OnSearchSubmitted);
            SearchBar.clearSearchButton.onClick.AddListener(OnSearchCleared);
            SearchBar.inputField.onSelect.AddListener(_ => SearchBarSelected?.Invoke());
            SearchBar.inputField.onDeselect.AddListener(_ => SearchBarDeselected?.Invoke());
            SortDropdown.Dropdown.onValueChanged.AddListener(index => SortChanged?.Invoke((ShopSortOption)index));
            ClearAllButton.onClick.AddListener(() => ClearAllClicked?.Invoke());
            RetryButton.onClick.AddListener(() => RetryClicked?.Invoke());
            BackToOverviewButton.onClick.AddListener(() => BackToOverviewClicked?.Invoke());
        }

        private void OnDestroy() =>
            searchCts.SafeCancelAndDispose();

        private void OnRectTransformDimensionsChange()
        {
            if (gridInitialized && isActiveAndEnabled)
                RecalculateColumns();
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
            RecalculateColumns();
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
            ResultCounterSkeleton.SetActive(on);
            ResultCounter.gameObject.SetActive(!on);
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

        public void SetCounter(int total, string? query) =>
            ResultCounter.text = query != null ? string.Format(COUNTER_SEARCH_FORMAT, total, query) : string.Format(COUNTER_FORMAT, total);

        public void SetSort(ShopSortOption sort) =>
            SortDropdown.Dropdown.SetValueWithoutNotify((int)sort);

        /// <summary>Sets the search text without raising SearchChanged.</summary>
        public void SetSearchText(string text)
        {
            searchCts.SafeCancelAndDispose();
            searchCts = null;
            SearchBar.inputField.SetTextWithoutNotify(text);
            SearchBar.clearSearchButton.gameObject.SetActive(!string.IsNullOrEmpty(text));
        }

        public ShopFilterChipView AddChip(string key, string label)
        {
            EnsureChipsPool();
            ShopFilterChipView chip = chipsPool!.Get();
            chip.Bind(key, label);
            activeChips.Add(chip);
            ChipsRow.SetActive(true);
            return chip;
        }

        public void ClearChips()
        {
            EnsureChipsPool();

            foreach (ShopFilterChipView chip in activeChips)
                chipsPool!.Release(chip);

            activeChips.Clear();
            ChipsRow.SetActive(false);
        }

        public void SetClearAllVisible(bool visible) =>
            ClearAllButton.gameObject.SetActive(visible);

        private void EnsureChipsPool()
        {
            chipsPool ??= new ObjectPool<ShopFilterChipView>(CreateChip, defaultCapacity: CHIPS_POOL_CAPACITY,
                actionOnGet: chip =>
                {
                    chip.gameObject.SetActive(true);
                    chip.transform.SetAsLastSibling();
                },
                actionOnRelease: chip => chip.gameObject.SetActive(false));
        }

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
                card.ViewClicked = c => CardViewClicked?.Invoke(c);
            }

            bindCard(card, model);

            if (index >= itemCount - (LOAD_MORE_LOOKAHEAD_ROWS * CurrentColumns))
                GridNearEnd?.Invoke();

            return item;
        }

        private void RecalculateColumns()
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)transform);
            float viewportWidth = Grid.ViewPortWidth;

            if (viewportWidth <= 0f)
                return;

            float usable = viewportWidth - Grid.Padding.left - Grid.Padding.right + Grid.ItemPadding.x;
            int columns = Mathf.Clamp(Mathf.FloorToInt(usable / (Grid.ItemSize.x + Grid.ItemPadding.x)), MIN_COLUMNS, MAX_COLUMNS);

            if (columns == CurrentColumns)
                return;

            CurrentColumns = columns;
            Grid.SetGridFixedGroupCount(GridFixedType.ColumnCountFixed, columns);
        }

        private void OnSearchValueChanged(string text)
        {
            SearchBar.clearSearchButton.gameObject.SetActive(!string.IsNullOrEmpty(text));
            searchCts = searchCts.SafeRestart();
            DebounceSearchAsync(text, searchCts.Token).Forget();
        }

        private void OnSearchSubmitted(string text)
        {
            searchCts.SafeCancelAndDispose();
            searchCts = null;
            SearchChanged?.Invoke(text);
        }

        private void OnSearchCleared()
        {
            SetSearchText(string.Empty);
            SearchChanged?.Invoke(string.Empty);
        }

        private async UniTaskVoid DebounceSearchAsync(string text, CancellationToken ct)
        {
            if (await UniTask.Delay(SEARCH_DEBOUNCE_MS, cancellationToken: ct).SuppressCancellationThrow())
                return;

            SearchChanged?.Invoke(text);
        }

        private ShopFilterChipView CreateChip()
        {
            ShopFilterChipView chip = Instantiate(ChipPrefab, ChipsContainer);
            chip.RemoveClicked = c => ChipRemoveClicked?.Invoke(c);
            return chip;
        }
    }
}
