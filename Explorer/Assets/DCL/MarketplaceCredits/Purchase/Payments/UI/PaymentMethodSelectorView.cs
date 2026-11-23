using System;
using TMPro;
using UnityEngine;

namespace DCL.MarketplaceCredits.Purchase.UI
{
    /// <summary>
    ///     The Credits and MANA rows of a purchase popup. It owns the buyer's ticks for the options it was last
    ///     shown, and re-renders both rows whenever a click changes them.
    /// </summary>
    public class PaymentMethodSelectorView : MonoBehaviour
    {
        private const string MANA_ALONE_REFUSED_NOTE = "Too low to pay with MANA alone";

        [field: SerializeField] public PaymentMethodRowView CreditsRow { get; private set; } = null!;
        [field: SerializeField] public PaymentMethodRowView ManaRow { get; private set; } = null!;
        [field: SerializeField] public TMP_Text RateText { get; private set; } = null!;

        public Action? SelectionChanged;

        private PaymentOptions options = PaymentOptions.NONE;

        public PaymentSelection Selection { get; private set; } = new (PaymentOptions.NONE);

        [JetBrains.Annotations.UsedImplicitly] // Unity event function
        private void Awake()
        {
            CreditsRow.Button.onClick.AddListener(OnCreditsClicked);
            ManaRow.Button.onClick.AddListener(OnManaClicked);
        }

        /// <summary>Starts a fresh selection on the preferred option. The MANA row is hidden when MANA cannot be offered at all.</summary>
        public void Show(PaymentOptions paymentOptions, bool isManaEnabled)
        {
            options = paymentOptions;
            Selection = new PaymentSelection(paymentOptions);
            ManaRow.gameObject.SetActive(isManaEnabled);
            Render();
        }

        /// <summary>The rows unavailable with unknown amounts, for the time the options are being read.</summary>
        public void Clear(bool isManaEnabled) =>
            Show(PaymentOptions.NONE, isManaEnabled);

        private void OnCreditsClicked()
        {
            if (Selection.ToggleCredits())
                OnSelectionChanged();
        }

        private void OnManaClicked()
        {
            if (Selection.ToggleMana())
                OnSelectionChanged();
        }

        private void OnSelectionChanged()
        {
            Render();
            SelectionChanged?.Invoke();
        }

        private void Render()
        {
            bool hasManaPrice = options.PriceManaWei.Sign > 0;

            CreditsRow.Render(
                Selection.CreditsAvailable,
                Selection.CreditsTicked,
                options.CreditsBalanceCents is { } balanceCents ? PaymentAmountFormat.Credits(balanceCents / PaymentOptionsCalculator.CENTS_PER_CREDIT) : PaymentAmountFormat.UNKNOWN,
                options.PriceCents > 0 ? PaymentAmountFormat.Credits(Selection.CreditsAmount) : PaymentAmountFormat.UNKNOWN);

            ManaRow.Render(
                Selection.ManaAvailable,
                Selection.ManaTicked,
                options.ManaBalanceWei is { } balanceWei ? PaymentAmountFormat.ManaBalance(balanceWei) : PaymentAmountFormat.UNKNOWN,
                hasManaPrice ? PaymentAmountFormat.ManaPrice(Selection.ManaAmountWei) : PaymentAmountFormat.UNKNOWN);

            RateText.gameObject.SetActive(hasManaPrice);

            // A MANA row greyed out by the relayer's minimum says so where the rate would be.
            if (hasManaPrice)
                RateText.text = options.IsManaAloneRelayable || Selection.ManaAvailable ? PaymentAmountFormat.Rate(options.PriceCents, options.PriceManaWei) : MANA_ALONE_REFUSED_NOTE;
        }
    }
}
