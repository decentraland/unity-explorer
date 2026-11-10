using DCL.MarketplaceCredits.Purchase.Names;
using DCL.UI;
using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.Shop
{
    public enum ShopNamesTab
    {
        Unique,
        Resale,
    }

    public class ShopNamesSectionView : MonoBehaviour
    {
        [field: Header("Tabs")]
        [field: SerializeField] public ButtonWithSelectableStateView UniqueTab { get; private set; } = null!;
        [field: SerializeField] public ButtonWithSelectableStateView ResaleTab { get; private set; } = null!;
        [field: SerializeField] public GameObject UniqueRoot { get; private set; } = null!;
        [field: SerializeField] public GameObject ResaleRoot { get; private set; } = null!;

        [field: Header("Unique name")]
        [field: SerializeField] public TMP_InputField NameInput { get; private set; } = null!;
        [field: SerializeField] public Image InputOutline { get; private set; } = null!;
        [field: SerializeField] public Color OutlineNormalColor { get; private set; } = Color.clear;
        [field: SerializeField] public Color OutlineErrorColor { get; private set; } = Color.red;
        [field: SerializeField] public TMP_Text HashLabel { get; private set; } = null!;
        [field: SerializeField] public Button ClaimButton { get; private set; } = null!;
        [field: SerializeField] public GameObject ClaimPriceRoot { get; private set; } = null!;
        [field: SerializeField] public TMP_Text ClaimPriceText { get; private set; } = null!;

        [field: Header("Status")]
        [field: SerializeField] public GameObject StatusRow { get; private set; } = null!;
        [field: SerializeField] public TMP_Text StatusText { get; private set; } = null!;
        [field: SerializeField] public Color StatusNeutralColor { get; private set; } = Color.white;
        [field: SerializeField] public Color StatusErrorColor { get; private set; } = Color.red;
        [field: SerializeField] public Button MakeOfferButton { get; private set; } = null!;

        public string Name => NameInput.text;

        public bool IsInputFocused => NameInput.isFocused;

        public event Action<ShopNamesTab>? TabClicked;
        public event Action<string>? NameChanged;
        public event Action? ClaimClicked;
        public event Action? MakeOfferClicked;
        public event Action? InputSelected;
        public event Action? InputDeselected;

        [JetBrains.Annotations.UsedImplicitly] // Unity event function
        private void Awake()
        {
            NameInput.characterLimit = NameRules.MAX_LENGTH;
            NameInput.onValidateInput = ValidateChar;
            NameInput.onValueChanged.AddListener(text => NameChanged?.Invoke(text));
            NameInput.onSelect.AddListener(_ => InputSelected?.Invoke());
            NameInput.onDeselect.AddListener(_ => InputDeselected?.Invoke());
            UniqueTab.Button.onClick.AddListener(() => TabClicked?.Invoke(ShopNamesTab.Unique));
            ResaleTab.Button.onClick.AddListener(() => TabClicked?.Invoke(ShopNamesTab.Resale));
            ClaimButton.onClick.AddListener(() => ClaimClicked?.Invoke());
            MakeOfferButton.onClick.AddListener(() => MakeOfferClicked?.Invoke());
        }

        public void ShowTab(ShopNamesTab tab)
        {
            UniqueTab.SetSelected(tab == ShopNamesTab.Unique);
            ResaleTab.SetSelected(tab == ShopNamesTab.Resale);
            UniqueRoot.SetActive(tab == ShopNamesTab.Unique);
            ResaleRoot.SetActive(tab == ShopNamesTab.Resale);
        }

        public void ClearName() =>
            NameInput.SetTextWithoutNotify(string.Empty);

        public void SetClaimEnabled(bool isEnabled) =>
            ClaimButton.interactable = isEnabled;

        public void SetClaimPrice(string price)
        {
            ClaimPriceRoot.SetActive(price.Length > 0);
            ClaimPriceText.text = price;
        }

        public void SetHash(string hash) =>
            HashLabel.text = hash;

        public void HideStatus()
        {
            StatusRow.SetActive(false);
            HashLabel.gameObject.SetActive(false);
            InputOutline.color = OutlineNormalColor;
        }

        public void ShowStatus(string text, bool isError, bool offerAvailable)
        {
            StatusRow.SetActive(true);
            StatusText.text = text;
            StatusText.color = isError ? StatusErrorColor : StatusNeutralColor;
            MakeOfferButton.gameObject.SetActive(offerAvailable);
            HashLabel.gameObject.SetActive(offerAvailable);
            InputOutline.color = isError ? OutlineErrorColor : OutlineNormalColor;
        }

        private static char ValidateChar(string text, int charIndex, char addedChar) =>
            NameRules.IsAllowedChar(addedChar) ? addedChar : '\0';
    }
}
