using DCL.UI;
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Pool;
using UnityEngine.UI;

namespace DCL.Shop
{
    public class ShopFiltersView : MonoBehaviour
    {
        private const int CATEGORY_ROWS_POOL_CAPACITY = 24;
        private const string SMART_HINT = "Smart wearables add interactive, in-world utility.";

        [field: Header("Sections")]
        [field: SerializeField] public ShopFilterSectionView CategorySection { get; private set; } = null!;
        [field: SerializeField] public ShopFilterSectionView PriceSection { get; private set; } = null!;
        [field: SerializeField] public ShopFilterSectionView RaritySection { get; private set; } = null!;
        [field: SerializeField] public ShopFilterSectionView StatusSection { get; private set; } = null!;

        [field: Header("Categories")]
        [field: SerializeField] public Transform CategoryRowsContainer { get; private set; } = null!;
        [field: SerializeField] public ShopCategoryRowView CategoryRowPrefab { get; private set; } = null!;

        [field: Header("Price")]
        [field: SerializeField] public TMP_InputField MinPriceInput { get; private set; } = null!;
        [field: SerializeField] public TMP_InputField MaxPriceInput { get; private set; } = null!;

        [field: Header("Rarity")]
        [field: SerializeField] public ShopRarityChipView[] RarityChips { get; private set; } = null!;

        [field: Header("Status")]
        [field: SerializeField] public Toggle StatusOnSaleToggle { get; private set; } = null!;
        [field: SerializeField] public Toggle StatusAllToggle { get; private set; } = null!;
        [field: SerializeField] public Toggle StatusNotForSaleToggle { get; private set; } = null!;

        [field: Header("Smart")]
        [field: SerializeField] public ToggleView SmartToggle { get; private set; } = null!;
        [field: SerializeField] public HoverableTooltip? SmartHint { get; private set; }

        public Func<string, Sprite?>? CategoryIconResolver;
        public Func<string, Color>? RarityColorResolver;

        private readonly List<ShopCategoryRowView> activeRows = new ();
        private readonly HashSet<string> expandedKeys = new ();
        private IObjectPool<ShopCategoryRowView>? rowsPool;
        private string selectedTop = ShopCategoryTree.ALL;
        private string? selectedSub;
        private bool applying;

        public event Action<string>? CategorySelected;
        public event Action<string?>? SubCategorySelected;
        public event Action<string, bool>? RarityToggled;
        public event Action<int?, int?>? PriceRangeChanged;
        public event Action<ShopStatusFilter>? StatusChanged;
        public event Action<bool>? SmartChanged;

        private void Awake()
        {
            EnsureRowsPool();
            MinPriceInput.onEndEdit.AddListener(OnPriceEdited);
            MaxPriceInput.onEndEdit.AddListener(OnPriceEdited);

            foreach (ShopRarityChipView chip in RarityChips)
                chip.Toggled = OnRarityChipToggled;

            StatusOnSaleToggle.onValueChanged.AddListener(isOn => OnStatusToggled(isOn, ShopStatusFilter.OnSale));
            StatusAllToggle.onValueChanged.AddListener(isOn => OnStatusToggled(isOn, ShopStatusFilter.All));
            StatusNotForSaleToggle.onValueChanged.AddListener(isOn => OnStatusToggled(isOn, ShopStatusFilter.NotForSale));
            SmartToggle.Toggle.onValueChanged.AddListener(OnSmartToggled);
            SmartHint?.Configure(SMART_HINT);
        }

        public void ApplyState(ShopCollectiblesFilters filters)
        {
            EnsureRowsPool();
            applying = true;

            selectedTop = filters.Category;
            selectedSub = filters.SubCategoryKey;
            expandedKeys.Clear();

            if (selectedTop != ShopCategoryTree.ALL)
                expandedKeys.Add(selectedTop);

            if (selectedSub != null && TryFindParent(selectedSub, out string? parentKey) && parentKey != selectedTop)
                expandedKeys.Add(parentKey!);

            RebuildTree();

            MinPriceInput.SetTextWithoutNotify(filters.MinPriceCredits?.ToString() ?? string.Empty);
            MaxPriceInput.SetTextWithoutNotify(filters.MaxPriceCredits?.ToString() ?? string.Empty);

            foreach (ShopRarityChipView chip in RarityChips)
            {
                if (RarityColorResolver != null)
                    chip.SetColor(RarityColorResolver(chip.RarityId));

                chip.SetSelectedSilently(filters.Rarities.Contains(chip.RarityId));
            }

            ShopStatusFilter status = filters.EffectiveStatus;
            StatusOnSaleToggle.SetIsOnWithoutNotify(status == ShopStatusFilter.OnSale);
            StatusAllToggle.SetIsOnWithoutNotify(status == ShopStatusFilter.All);
            StatusNotForSaleToggle.SetIsOnWithoutNotify(status == ShopStatusFilter.NotForSale);
            SmartToggle.Toggle.SetIsOnWithoutNotify(filters.Smart);

            applying = false;
        }

        private void EnsureRowsPool()
        {
            rowsPool ??= new ObjectPool<ShopCategoryRowView>(CreateRow, defaultCapacity: CATEGORY_ROWS_POOL_CAPACITY,
                actionOnGet: row =>
                {
                    row.gameObject.SetActive(true);
                    row.transform.SetAsLastSibling();
                },
                actionOnRelease: row => row.gameObject.SetActive(false));
        }

        private void RebuildTree()
        {
            foreach (ShopCategoryRowView row in activeRows)
                rowsPool!.Release(row);

            activeRows.Clear();

            foreach (ShopCategoryTree.Node top in ShopCategoryTree.TOP)
            {
                bool topSelected = top.Key == selectedTop && selectedSub == null;
                bool topExpanded = expandedKeys.Contains(top.Key);
                AddRow(top, 0, topSelected, topExpanded);

                if (!topExpanded)
                    continue;

                foreach (ShopCategoryTree.Node child in top.Children)
                {
                    bool childExpanded = expandedKeys.Contains(child.Key);
                    AddRow(child, 1, child.Key == selectedSub, childExpanded);

                    if (!childExpanded)
                        continue;

                    foreach (ShopCategoryTree.Node grandChild in child.Children)
                        AddRow(grandChild, 2, grandChild.Key == selectedSub, false);
                }
            }
        }

        private void AddRow(ShopCategoryTree.Node node, int depth, bool selected, bool expanded)
        {
            ShopCategoryRowView row = rowsPool!.Get();
            Sprite? icon = node.IconCategory != null && CategoryIconResolver != null ? CategoryIconResolver(node.IconCategory) : null;
            row.Bind(node, depth, selected, expanded, icon);
            activeRows.Add(row);
        }

        private void OnRowClicked(ShopCategoryRowView row)
        {
            ShopCategoryTree.Node? node = row.Node;

            if (node == null)
                return;

            if (IsTopLevel(node))
            {
                selectedSub = null;

                if (node.Key == ShopCategoryTree.ALL)
                {
                    selectedTop = ShopCategoryTree.ALL;
                    expandedKeys.Clear();
                }
                else
                {
                    if (selectedTop == node.Key && expandedKeys.Contains(node.Key))
                        expandedKeys.Remove(node.Key);
                    else
                        expandedKeys.Add(node.Key);

                    selectedTop = node.Key;
                }

                RebuildTree();
                CategorySelected?.Invoke(selectedTop);
                return;
            }

            selectedSub = selectedSub == node.Key ? null : node.Key;

            if (node.HasChildren)
            {
                if (!expandedKeys.Remove(node.Key))
                    expandedKeys.Add(node.Key);
            }

            RebuildTree();
            SubCategorySelected?.Invoke(selectedSub);
        }

        private static bool IsTopLevel(ShopCategoryTree.Node node)
        {
            foreach (ShopCategoryTree.Node top in ShopCategoryTree.TOP)
            {
                if (top == node)
                    return true;
            }

            return false;
        }

        private static bool TryFindParent(string key, out string? parentKey)
        {
            foreach (ShopCategoryTree.Node top in ShopCategoryTree.TOP)
            {
                foreach (ShopCategoryTree.Node child in top.Children)
                {
                    if (child.Key == key)
                    {
                        parentKey = top.Key;
                        return true;
                    }

                    foreach (ShopCategoryTree.Node grandChild in child.Children)
                    {
                        if (grandChild.Key != key)
                            continue;

                        parentKey = child.Key;
                        return true;
                    }
                }
            }

            parentKey = null;
            return false;
        }

        private void OnPriceEdited(string _)
        {
            if (applying)
                return;

            PriceRangeChanged?.Invoke(ParsePrice(MinPriceInput), ParsePrice(MaxPriceInput));
        }

        private static int? ParsePrice(TMP_InputField input)
        {
            if (int.TryParse(input.text, out int value) && value > 0)
                return value;

            input.SetTextWithoutNotify(string.Empty);
            return null;
        }

        private void OnRarityChipToggled(ShopRarityChipView chip, bool isOn)
        {
            if (!applying)
                RarityToggled?.Invoke(chip.RarityId, isOn);
        }

        private void OnStatusToggled(bool isOn, ShopStatusFilter status)
        {
            if (isOn && !applying)
                StatusChanged?.Invoke(status);
        }

        private void OnSmartToggled(bool isOn)
        {
            if (!applying)
                SmartChanged?.Invoke(isOn);
        }

        private ShopCategoryRowView CreateRow()
        {
            ShopCategoryRowView row = Instantiate(CategoryRowPrefab, CategoryRowsContainer);
            row.Clicked = OnRowClicked;
            return row;
        }
    }
}
