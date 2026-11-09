using Cysharp.Threading.Tasks;
using DG.Tweening;
using MVC;
using System.Threading;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.MarketplaceCredits.Purchase.Cart.UI
{
    public class ShopCartModalView : ViewBase, IView
    {
        private const float POPUP_SHOW_ANIMATION_TIME = 0.3f;
        private const float POPUP_HIDE_ANIMATION_TIME = 0.2f;

        [field: SerializeField] public RectTransform ContainerTransform { get; private set; } = null!;
        [field: SerializeField] public TMP_Text TitleText { get; private set; } = null!;

        [field: Header("Lines")]
        [field: SerializeField] public Transform LinesContainer { get; private set; } = null!;
        [field: SerializeField] public ShopCartLineView LinePrefab { get; private set; } = null!;
        [field: SerializeField] public GameObject EmptyState { get; private set; } = null!;
        [field: SerializeField] public TMP_Text TotalCreditsText { get; private set; } = null!;

        [field: Header("Balance")]
        [field: SerializeField] public Button BuyCreditsButton { get; private set; } = null!;

        [field: Header("Actions")]
        [field: SerializeField] public Button CheckoutButton { get; private set; } = null!;
        [field: SerializeField] public Button ConfirmChangesButton { get; private set; } = null!;
        [field: SerializeField] public Button BackToCartButton { get; private set; } = null!;
        [field: SerializeField] public Button ToBackpackButton { get; private set; } = null!;
        [field: SerializeField] public Button ContinueShoppingButton { get; private set; } = null!;
        [field: SerializeField] public Button RetryButton { get; private set; } = null!;
        [field: SerializeField] public Button DoneButton { get; private set; } = null!;
        [field: SerializeField] public Button CloseButton { get; private set; } = null!;
        [field: SerializeField] public Button CloseBackground { get; private set; } = null!;

        [field: Header("States")]
        [field: SerializeField] public GameObject LinesStateContainer { get; private set; } = null!;
        [field: SerializeField] public GameObject ReviewingSpinner { get; private set; } = null!;
        [field: SerializeField] public GameObject ConfirmChangesContainer { get; private set; } = null!;
        [field: SerializeField] public TMP_Text ChangesSummaryText { get; private set; } = null!;
        [field: SerializeField] public GameObject InsufficientCreditsContainer { get; private set; } = null!;
        [field: SerializeField] public TMP_Text ShortfallText { get; private set; } = null!;
        [field: SerializeField] public GameObject ProgressStateContainer { get; private set; } = null!;
        [field: SerializeField] public TMP_Text ProgressStatusText { get; private set; } = null!;
        [field: SerializeField] public GameObject SuccessStateContainer { get; private set; } = null!;
        [field: SerializeField] public TMP_Text SuccessSummaryText { get; private set; } = null!;
        [field: SerializeField] public GameObject PartialFailureStateContainer { get; private set; } = null!;
        [field: SerializeField] public TMP_Text PartialSummaryText { get; private set; } = null!;
        [field: SerializeField] public GameObject FailedStateContainer { get; private set; } = null!;
        [field: SerializeField] public TMP_Text FailedReasonText { get; private set; } = null!;
        [field: SerializeField] public GameObject SignatureCountBadge { get; private set; } = null!;
        [field: SerializeField] public TMP_Text SignatureCountText { get; private set; } = null!;

        /// <summary>
        ///     Hangs the container's top-right corner <paramref name="gap" /> under <paramref name="worldAnchor" />, kept
        ///     inside the parent rect so the popup never leaves the screen.
        /// </summary>
        public void PositionBelow(Vector3 worldAnchor, float gap)
        {
            var parent = (RectTransform)ContainerTransform.parent;
            Vector3 anchor = parent.InverseTransformPoint(worldAnchor);
            Rect rect = ContainerTransform.rect;
            Rect bounds = parent.rect;

            // rect.xMax/yMax are the container's top-right edges relative to its pivot, so subtracting them turns the
            // wanted corner position into a pivot position
            float x = Mathf.Clamp(anchor.x - rect.xMax, bounds.xMin - rect.xMin, bounds.xMax - rect.xMax);
            float y = Mathf.Clamp(anchor.y - gap - rect.yMax, bounds.yMin - rect.yMin, bounds.yMax - rect.yMax);
            ContainerTransform.localPosition = new Vector3(x, y, 0f);
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
