using Cysharp.Threading.Tasks;
using DCL.MarketplaceCredits.Purchase.UI;
using DG.Tweening;
using MVC;
using System;
using System.Threading;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.MarketplaceCredits.Purchase.Names.UI
{
    public class NameClaimModalView : ViewBase, IView
    {
        private const float POPUP_SHOW_ANIMATION_TIME = 0.3f;
        private const float POPUP_HIDE_ANIMATION_TIME = 0.2f;

        [field: SerializeField] public RectTransform ContainerTransform { get; private set; } = null!;
        [field: SerializeField] public Button CloseButton { get; private set; } = null!;
        [field: SerializeField] public Button CloseBackground { get; private set; } = null!;

        [field: Header("Name and price")]
        [field: SerializeField] public TMP_Text[] NameTexts { get; private set; } = Array.Empty<TMP_Text>();
        [field: SerializeField] public TMP_Text PriceCreditsText { get; private set; } = null!;
        [field: SerializeField] public GameObject PriceLoadingSpinner { get; private set; } = null!;

        [field: Header("Confirm")]
        [field: SerializeField] public GameObject ConfirmStateContainer { get; private set; } = null!;
        [field: SerializeField] public TMP_Text TitleText { get; private set; } = null!;
        [field: SerializeField] public TMP_Text BalanceCreditsText { get; private set; } = null!;
        [field: SerializeField] public GameObject BalanceManaGroup { get; private set; } = null!;
        [field: SerializeField] public TMP_Text BalanceManaText { get; private set; } = null!;
        [field: SerializeField] public PaymentMethodSelectorView PaymentSelector { get; private set; } = null!;
        [field: SerializeField] public Button ConfirmButton { get; private set; } = null!;
        [field: SerializeField] public Button CancelButton { get; private set; } = null!;

        [field: Header("Insufficient funds")]
        [field: SerializeField] public GameObject InsufficientIcon { get; private set; } = null!;
        [field: SerializeField] public TMP_Text CannotAffordText { get; private set; } = null!;
        [field: SerializeField] public Button GetCreditsButton { get; private set; } = null!;

        [field: Header("Progress")]
        [field: SerializeField] public GameObject ProgressStateContainer { get; private set; } = null!;
        [field: SerializeField] public TMP_Text ProgressStatusText { get; private set; } = null!;

        [field: Header("Success")]
        [field: SerializeField] public GameObject SuccessStateContainer { get; private set; } = null!;
        [field: SerializeField] public TMP_Text SuccessText { get; private set; } = null!;
        [field: SerializeField] public Button ContinueShoppingButton { get; private set; } = null!;

        [field: Header("Result")]
        [field: SerializeField] public GameObject ResultStateContainer { get; private set; } = null!;
        [field: SerializeField] public TMP_Text ResultTitleText { get; private set; } = null!;
        [field: SerializeField] public TMP_Text ResultText { get; private set; } = null!;
        [field: SerializeField] public Button RetryButton { get; private set; } = null!;
        [field: SerializeField] public Button DoneButton { get; private set; } = null!;

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
