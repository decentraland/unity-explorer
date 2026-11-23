using Cysharp.Threading.Tasks;
using DCL.MarketplaceCredits.Purchase.UI;
using DG.Tweening;
using MVC;
using System.Threading;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.MarketplaceCredits.Purchase.Cart.UI
{
    public class ShopCartCheckoutModalView : ViewBase, IView
    {
        private const float POPUP_SHOW_ANIMATION_TIME = 0.3f;
        private const float POPUP_HIDE_ANIMATION_TIME = 0.2f;

        [field: SerializeField] public RectTransform ContainerTransform { get; private set; } = null!;

        [field: Header("Header")]
        [field: SerializeField] public GameObject InsufficientIcon { get; private set; } = null!;
        [field: SerializeField] public TMP_Text TitleText { get; private set; } = null!;
        [field: SerializeField] public TMP_Text BalanceCreditsText { get; private set; } = null!;
        [field: SerializeField] public GameObject BalanceManaGroup { get; private set; } = null!;
        [field: SerializeField] public TMP_Text BalanceManaText { get; private set; } = null!;
        [field: SerializeField] public TMP_Text InsufficientText { get; private set; } = null!;
        [field: SerializeField] public TMP_Text ChangesNoticeText { get; private set; } = null!;

        [field: Header("Lines")]
        [field: SerializeField] public Transform LinesContainer { get; private set; } = null!;
        [field: SerializeField] public ShopCartCheckoutLineView LinePrefab { get; private set; } = null!;
        [field: SerializeField] public LayoutElement LinesScrollLayout { get; private set; } = null!;
        [field: SerializeField] public float LineHeight { get; private set; } = 96f;
        [field: SerializeField] public float LineSpacing { get; private set; } = 8f;
        [field: SerializeField] public int MaxVisibleLines { get; private set; } = 2;

        [field: Header("Payment")]
        [field: SerializeField] public PaymentMethodSelectorView PaymentSelector { get; private set; } = null!;
        [field: SerializeField] public TMP_Text TotalItemsText { get; private set; } = null!;
        [field: SerializeField] public GameObject TotalCreditsGroup { get; private set; } = null!;
        [field: SerializeField] public TMP_Text TotalCreditsText { get; private set; } = null!;
        [field: SerializeField] public GameObject TotalPlus { get; private set; } = null!;
        [field: SerializeField] public GameObject TotalManaGroup { get; private set; } = null!;
        [field: SerializeField] public TMP_Text TotalManaText { get; private set; } = null!;

        [field: Header("Actions")]
        [field: SerializeField] public Button BuyButton { get; private set; } = null!;
        [field: SerializeField] public Button BuyCreditsButton { get; private set; } = null!;
        [field: SerializeField] public Button CancelButton { get; private set; } = null!;
        [field: SerializeField] public Button CancelConfirmationButton { get; private set; } = null!;
        [field: SerializeField] public Button ToBackpackButton { get; private set; } = null!;
        [field: SerializeField] public Button ContinueShoppingButton { get; private set; } = null!;
        [field: SerializeField] public Button RetryButton { get; private set; } = null!;
        [field: SerializeField] public Button DoneButton { get; private set; } = null!;
        [field: SerializeField] public Button CloseButton { get; private set; } = null!;
        [field: SerializeField] public Button CloseBackground { get; private set; } = null!;

        [field: Header("States")]
        [field: SerializeField] public GameObject ReviewingContainer { get; private set; } = null!;
        [field: SerializeField] public GameObject ConfirmStateContainer { get; private set; } = null!;
        [field: SerializeField] public GameObject ProgressStateContainer { get; private set; } = null!;
        [field: SerializeField] public TMP_Text ProgressStatusText { get; private set; } = null!;
        [field: SerializeField] public GameObject SignatureCountBadge { get; private set; } = null!;
        [field: SerializeField] public TMP_Text SignatureCountText { get; private set; } = null!;
        [field: SerializeField] public GameObject SuccessStateContainer { get; private set; } = null!;
        [field: SerializeField] public TMP_Text SuccessSummaryText { get; private set; } = null!;
        [field: SerializeField] public GameObject PartialFailureStateContainer { get; private set; } = null!;
        [field: SerializeField] public TMP_Text PartialSummaryText { get; private set; } = null!;
        [field: SerializeField] public GameObject FailedStateContainer { get; private set; } = null!;
        [field: SerializeField] public TMP_Text FailedReasonText { get; private set; } = null!;

        /// <summary>Sizes the list to its rows, up to <see cref="MaxVisibleLines" />; more than that scrolls.</summary>
        public void SetVisibleLines(int lineCount)
        {
            int visible = Mathf.Clamp(lineCount, 1, MaxVisibleLines);
            LinesScrollLayout.preferredHeight = (visible * LineHeight) + ((visible - 1) * LineSpacing);
        }

        protected override UniTask PlayShowAnimationAsync(CancellationToken ct)
        {
            ContainerTransform.DOKill();
            ContainerTransform.localScale = Vector3.zero;
            return ContainerTransform.DOScale(Vector3.one, POPUP_SHOW_ANIMATION_TIME).SetEase(Ease.OutBack).ToUniTask(cancellationToken: ct);
        }

        protected override UniTask PlayHideAnimationAsync(CancellationToken ct)
        {
            ContainerTransform.DOKill();
            return ContainerTransform.DOScale(Vector3.zero, POPUP_HIDE_ANIMATION_TIME).SetEase(Ease.InBack).ToUniTask(cancellationToken: ct);
        }
    }
}
