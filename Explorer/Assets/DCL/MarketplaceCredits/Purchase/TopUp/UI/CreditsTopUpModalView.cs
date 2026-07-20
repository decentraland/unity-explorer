using MVC;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.MarketplaceCredits.Purchase.TopUp.UI
{
    public class CreditsTopUpModalView : ViewBase, IView
    {
        [field: Header("Packs")]
        [field: SerializeField] public CreditsTopUpPackItemView[] PackItems { get; private set; } = null!;

        [field: Header("Balance")]
        [field: SerializeField] public TMP_Text BalanceCreditsText { get; private set; } = null!;
        [field: SerializeField] public GameObject BalanceLoadingSpinner { get; private set; } = null!;

        [field: Header("Actions")]
        [field: SerializeField] public Button CloseButton { get; private set; } = null!;
        [field: SerializeField] public Button DoneButton { get; private set; } = null!;

        [field: Header("States")]
        [field: SerializeField] public GameObject PackSelectionContainer { get; private set; } = null!;
        [field: SerializeField] public GameObject CheckoutSpinner { get; private set; } = null!;
        [field: SerializeField] public GameObject WaitingForBrowserContainer { get; private set; } = null!;
        [field: SerializeField] public GameObject PendingContainer { get; private set; } = null!;
        [field: SerializeField] public GameObject SuccessContainer { get; private set; } = null!;
        [field: SerializeField] public TMP_Text SuccessCreditsText { get; private set; } = null!;
        [field: SerializeField] public TMP_Text SuccessNewBalanceText { get; private set; } = null!;
        [field: SerializeField] public GameObject FailedContainer { get; private set; } = null!;
        [field: SerializeField] public TMP_Text FailedReasonText { get; private set; } = null!;
        [field: SerializeField] public Button RetryButton { get; private set; } = null!;
    }
}
