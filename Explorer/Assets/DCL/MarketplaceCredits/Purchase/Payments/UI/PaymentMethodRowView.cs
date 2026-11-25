using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.MarketplaceCredits.Purchase.UI
{
    /// <summary>
    ///     One tickable currency row: the tick box, the balance held and the amount this currency would pay.
    ///     A row that cannot pay is dimmed and not clickable.
    /// </summary>
    public class PaymentMethodRowView : MonoBehaviour
    {
        [field: SerializeField] public Button Button { get; private set; } = null!;
        [field: SerializeField] public CanvasGroup CanvasGroup { get; private set; } = null!;
        [field: SerializeField] public GameObject CheckMark { get; private set; } = null!;
        [field: SerializeField] public GameObject SelectedOutline { get; private set; } = null!;
        [field: SerializeField] public TMP_Text BalanceText { get; private set; } = null!;
        [field: SerializeField] public TMP_Text AmountText { get; private set; } = null!;
        [field: SerializeField] public float UnavailableAlpha { get; private set; } = 0.4f;

        public void Render(bool available, bool ticked, string balance, string amount)
        {
            Button.interactable = available;
            CanvasGroup.alpha = available ? 1f : UnavailableAlpha;
            CheckMark.SetActive(ticked);
            SelectedOutline.SetActive(ticked);
            BalanceText.text = balance;
            AmountText.text = amount;
        }
    }
}
