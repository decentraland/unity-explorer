using Cysharp.Threading.Tasks;
using DG.Tweening;
using MVC;
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
        [field: SerializeField] public TMP_Text NameText { get; private set; } = null!;
        [field: SerializeField] public TMP_Text PriceCreditsText { get; private set; } = null!;
        [field: SerializeField] public GameObject PriceLoadingSpinner { get; private set; } = null!;
        [field: SerializeField] public TMP_Text BalanceCreditsText { get; private set; } = null!;

        [field: Header("Confirm")]
        [field: SerializeField] public GameObject ConfirmStateContainer { get; private set; } = null!;
        [field: SerializeField] public Button ConfirmButton { get; private set; } = null!;
        [field: SerializeField] public Button CancelButton { get; private set; } = null!;

        [field: Header("Insufficient credits")]
        [field: SerializeField] public GameObject InsufficientCreditsContainer { get; private set; } = null!;
        [field: SerializeField] public TMP_Text CannotAffordText { get; private set; } = null!;
        [field: SerializeField] public Button GetCreditsButton { get; private set; } = null!;

        [field: Header("Progress")]
        [field: SerializeField] public GameObject ProgressStateContainer { get; private set; } = null!;
        [field: SerializeField] public TMP_Text ProgressStatusText { get; private set; } = null!;

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
