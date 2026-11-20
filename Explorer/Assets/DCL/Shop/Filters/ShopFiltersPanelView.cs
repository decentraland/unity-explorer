using Cysharp.Threading.Tasks;
using DCL.Backpack.Gifting.Styling;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Utility;

namespace DCL.Shop
{
    /// <summary>
    ///     The header FILTERS button and the panel it opens: price range, one rarity, status and the deals / smart checkboxes.
    /// </summary>
    public class ShopFiltersPanelView : MonoBehaviour
    {
        private const int PRICE_DEBOUNCE_MS = 500;
        private const float UNSELECTED_RARITY_ALPHA = 0.35f;
        private const float DISABLED_GROUP_ALPHA = 0.4f;

        // The order the pills are shown in, which is not the order the feed sorts rarities by
        private static readonly string[] RARITY_DISPLAY_ORDER = { "common", "uncommon", "rare", "epic", "legendary", "exotic", "mythic", "unique" };
        private static readonly Vector3[] CORNERS = new Vector3[4];

        [field: SerializeField] private Button ToggleButton { get; set; } = null!;
        [field: SerializeField] private RectTransform Panel { get; set; } = null!;
        [field: SerializeField] private Button CloseArea { get; set; } = null!;
        [field: SerializeField] private float PanelGap { get; set; } = 8f;

        [field: Header("Price")]
        [field: SerializeField] private CanvasGroup PriceGroup { get; set; } = null!;
        [field: SerializeField] private TMP_InputField MinInput { get; set; } = null!;
        [field: SerializeField] private TMP_InputField MaxInput { get; set; } = null!;
        [field: SerializeField] private Slider MinSlider { get; set; } = null!;
        [field: SerializeField] private Slider MaxSlider { get; set; } = null!;
        [field: SerializeField] private RectTransform RangeFill { get; set; } = null!;

        [field: Header("Rarity")]
        [field: SerializeField] private ShopRarityFilterPillView RarityPillTemplate { get; set; } = null!;

        [field: Header("Status")]
        [field: SerializeField] private Toggle StatusAllToggle { get; set; } = null!;
        [field: SerializeField] private Toggle StatusOnSaleToggle { get; set; } = null!;
        [field: SerializeField] private Toggle StatusNotForSaleToggle { get; set; } = null!;

        [field: Header("Show only")]
        [field: SerializeField] private CanvasGroup DealsGroup { get; set; } = null!;
        [field: SerializeField] private Toggle DealsToggle { get; set; } = null!;
        [field: SerializeField] private GameObject SmartRow { get; set; } = null!;
        [field: SerializeField] private Toggle SmartToggle { get; set; } = null!;

        private readonly List<ShopRarityFilterPillView> rarityPills = new ();
        private readonly List<Color> rarityColors = new ();

        private CancellationTokenSource? priceCts;
        private string? selectedRarity;
        private int? minCredits;
        private int? maxCredits;

        public event Action<int?, int?>? PriceRangeChanged;
        public event Action<string?>? RaritySelected;
        public event Action<ShopStatusFilter>? StatusChanged;
        public event Action<bool>? DealsChanged;
        public event Action<bool>? SmartChanged;
        public event Action? InputSelected;
        public event Action? InputDeselected;

        [JetBrains.Annotations.UsedImplicitly] // Unity event function
        private void OnDisable() =>
            Close();

        [JetBrains.Annotations.UsedImplicitly] // Unity event function
        private void OnDestroy() =>
            priceCts.SafeCancelAndDispose();

        /// <summary>Wires the controls and creates the rarity pills, once.</summary>
        public void Initialize(IWearableStylingCatalog styling)
        {
            if (rarityPills.Count > 0)
                return;

            ToggleButton.onClick.AddListener(ToggleOpen);
            CloseArea.onClick.AddListener(Close);

            MinSlider.minValue = MaxSlider.minValue = 0;
            MinSlider.maxValue = MaxSlider.maxValue = ShopPriceSliderScale.STEPS;
            MinSlider.wholeNumbers = MaxSlider.wholeNumbers = true;
            MinSlider.onValueChanged.AddListener(OnMinSliderChanged);
            MaxSlider.onValueChanged.AddListener(OnMaxSliderChanged);

            WireInput(MinInput);
            WireInput(MaxInput);

            StatusAllToggle.onValueChanged.AddListener(isOn => OnStatusToggled(isOn, ShopStatusFilter.All));
            StatusOnSaleToggle.onValueChanged.AddListener(isOn => OnStatusToggled(isOn, ShopStatusFilter.OnSale));
            StatusNotForSaleToggle.onValueChanged.AddListener(isOn => OnStatusToggled(isOn, ShopStatusFilter.NotForSale));
            DealsToggle.onValueChanged.AddListener(isOn => DealsChanged?.Invoke(isOn));
            SmartToggle.onValueChanged.AddListener(isOn => SmartChanged?.Invoke(isOn));

            for (var i = 0; i < RARITY_DISPLAY_ORDER.Length; i++)
            {
                string rarity = RARITY_DISPLAY_ORDER[i];
                ShopRarityFilterPillView pill = i == 0 ? RarityPillTemplate : Instantiate(RarityPillTemplate, RarityPillTemplate.transform.parent);
                pill.name = rarity;
                pill.Label.text = rarity.ToUpperInvariant();
                pill.Button.onClick.AddListener(() => OnRarityClicked(rarity));
                rarityPills.Add(pill);
                rarityColors.Add(styling.GetRarityFlapColor(rarity));
            }

            RefreshRarityPills();
            SetOpen(false);
        }

        /// <summary>Shows the given filters on every control without raising any event.</summary>
        public void ApplyState(ShopGridFilters filters, bool smartAvailable)
        {
            priceCts.SafeCancelAndDispose();
            priceCts = null;

            minCredits = filters.MinPriceCredits;
            maxCredits = filters.MaxPriceCredits;
            RefreshPriceInputs();
            RefreshPriceSliders();

            selectedRarity = filters.Rarities.Count > 0 ? filters.Rarities[0] : null;
            RefreshRarityPills();

            ShopStatusFilter status = filters.EffectiveStatus;
            StatusAllToggle.SetIsOnWithoutNotify(status == ShopStatusFilter.All);
            StatusOnSaleToggle.SetIsOnWithoutNotify(status == ShopStatusFilter.OnSale);
            StatusNotForSaleToggle.SetIsOnWithoutNotify(status == ShopStatusFilter.NotForSale);

            DealsToggle.SetIsOnWithoutNotify(filters.Deals);
            SmartToggle.SetIsOnWithoutNotify(filters.Smart);
            SmartRow.SetActive(smartAvailable);

            // Only the on-sale feed takes a price range or the deals flag
            SetGroupEnabled(PriceGroup, filters.UsesUnifiedFeed);
            SetGroupEnabled(DealsGroup, filters.UsesUnifiedFeed);
        }

        public void Close() =>
            SetOpen(false);

        private void ToggleOpen() =>
            SetOpen(!Panel.gameObject.activeSelf);

        private void SetOpen(bool open)
        {
            // A focused field that is hidden never reports its deselection
            if (!open && (MinInput.isFocused || MaxInput.isFocused))
                InputDeselected?.Invoke();

            CloseArea.gameObject.SetActive(open);
            Panel.gameObject.SetActive(open);

            if (!open)
                return;

            ((RectTransform)ToggleButton.transform).GetWorldCorners(CORNERS);
            Panel.position = CORNERS[0];
            Panel.anchoredPosition -= new Vector2(0f, PanelGap);
        }

        private void WireInput(TMP_InputField input)
        {
            input.contentType = TMP_InputField.ContentType.IntegerNumber;
            input.onEndEdit.AddListener(_ => OnPriceInputCommitted());
            input.onSelect.AddListener(_ => InputSelected?.Invoke());
            input.onDeselect.AddListener(_ => InputDeselected?.Invoke());
        }

        private void OnPriceInputCommitted()
        {
            int? min = ParseBound(MinInput.text);
            int? max = ParseBound(MaxInput.text);

            if (min.HasValue && max.HasValue && max.Value < min.Value)
                max = min;

            priceCts.SafeCancelAndDispose();
            priceCts = null;
            minCredits = min;
            maxCredits = max;
            RefreshPriceInputs();
            RefreshPriceSliders();
            PriceRangeChanged?.Invoke(minCredits, maxCredits);
        }

        private void OnMinSliderChanged(float value)
        {
            if (value > MaxSlider.value)
            {
                MinSlider.SetValueWithoutNotify(MaxSlider.value);
                value = MaxSlider.value;
            }

            var step = (int)value;
            minCredits = step <= 0 ? null : ShopPriceSliderScale.StepToCredits(step);
            OnSliderMoved();
        }

        private void OnMaxSliderChanged(float value)
        {
            if (value < MinSlider.value)
            {
                MaxSlider.SetValueWithoutNotify(MinSlider.value);
                value = MinSlider.value;
            }

            var step = (int)value;
            maxCredits = step >= ShopPriceSliderScale.STEPS ? null : ShopPriceSliderScale.StepToCredits(step);
            OnSliderMoved();
        }

        private void OnSliderMoved()
        {
            RefreshPriceInputs();
            RefreshRangeFill();
            priceCts = priceCts.SafeRestart();
            DebouncePriceAsync(priceCts.Token).Forget();
        }

        private async UniTaskVoid DebouncePriceAsync(CancellationToken ct)
        {
            if (await UniTask.Delay(PRICE_DEBOUNCE_MS, cancellationToken: ct).SuppressCancellationThrow())
                return;

            PriceRangeChanged?.Invoke(minCredits, maxCredits);
        }

        private void RefreshPriceInputs()
        {
            MinInput.SetTextWithoutNotify(minCredits.HasValue ? minCredits.Value.ToString(CultureInfo.InvariantCulture) : string.Empty);
            MaxInput.SetTextWithoutNotify(maxCredits.HasValue ? maxCredits.Value.ToString(CultureInfo.InvariantCulture) : string.Empty);
        }

        private void RefreshPriceSliders()
        {
            MinSlider.SetValueWithoutNotify(minCredits.HasValue ? ShopPriceSliderScale.CreditsToStep(minCredits.Value) : 0);
            MaxSlider.SetValueWithoutNotify(maxCredits.HasValue ? ShopPriceSliderScale.CreditsToStep(maxCredits.Value) : ShopPriceSliderScale.STEPS);
            RefreshRangeFill();
        }

        private void RefreshRangeFill()
        {
            RangeFill.anchorMin = new Vector2(MinSlider.normalizedValue, RangeFill.anchorMin.y);
            RangeFill.anchorMax = new Vector2(MaxSlider.normalizedValue, RangeFill.anchorMax.y);

            // Two handles resting together at the far end leave only the top one reachable, and that one must be able to move left
            bool minOnTop = MinSlider.value >= MaxSlider.value && MaxSlider.normalizedValue > 0.5f;
            (minOnTop ? MinSlider : MaxSlider).transform.SetAsLastSibling();
        }

        private void OnRarityClicked(string rarity)
        {
            selectedRarity = selectedRarity == rarity ? null : rarity;
            RefreshRarityPills();
            RaritySelected?.Invoke(selectedRarity);
        }

        private void RefreshRarityPills()
        {
            for (var i = 0; i < rarityPills.Count; i++)
            {
                Color color = rarityColors[i];
                color.a = selectedRarity == null || selectedRarity == RARITY_DISPLAY_ORDER[i] ? 1f : UNSELECTED_RARITY_ALPHA;
                rarityPills[i].Background.color = color;
            }
        }

        private void OnStatusToggled(bool isOn, ShopStatusFilter status)
        {
            if (isOn)
                StatusChanged?.Invoke(status);
        }

        private static void SetGroupEnabled(CanvasGroup group, bool enabled)
        {
            group.alpha = enabled ? 1f : DISABLED_GROUP_ALPHA;
            group.interactable = enabled;
            group.blocksRaycasts = enabled;
        }

        private static int? ParseBound(string text) =>
            int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value) && value > 0 ? value : null;
    }
}
