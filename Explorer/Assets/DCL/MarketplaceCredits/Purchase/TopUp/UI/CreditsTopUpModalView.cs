using MVC;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.MarketplaceCredits.Purchase.TopUp.UI
{
    public class CreditsTopUpModalView : ViewBase, IView
    {
        [field: Header("Chrome")]
        [field: SerializeField] public GameObject HeaderContainer { get; private set; } = null!;

        [field: Header("Packs")]
        [field: SerializeField] public CreditsTopUpPackItemView[] PackItems { get; private set; } = null!;

        [field: Header("Balance")]
        [field: SerializeField] public GameObject BalanceContainer { get; private set; } = null!;
        [field: SerializeField] public TMP_Text BalanceCreditsText { get; private set; } = null!;
        [field: SerializeField] public GameObject BalanceLoadingSpinner { get; private set; } = null!;

        [field: Header("Actions")]
        [field: SerializeField] public Button CloseButton { get; private set; } = null!;
        [field: SerializeField] public Button DoneButton { get; private set; } = null!;

        [field: Header("States")]
        [field: SerializeField] public GameObject PackSelectionContainer { get; private set; } = null!;
        [field: SerializeField] public GameObject WaitingForBrowserContainer { get; private set; } = null!;
        [field: SerializeField] public GameObject SuccessContainer { get; private set; } = null!;
        [field: SerializeField] public GameObject FailedContainer { get; private set; } = null!;
        [field: SerializeField] public TMP_Text ResultText { get; private set; } = null!;
        [field: SerializeField] public TMP_Text FailedReasonText { get; private set; } = null!;
        [field: SerializeField] public Button RetryButton { get; private set; } = null!;
    }
}
