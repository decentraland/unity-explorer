using Cysharp.Threading.Tasks;
using DG.Tweening;
using MVC;
using System.Threading;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.MarketplaceCredits.Purchase.TopUp.UI
{
    public class CreditsTopUpModalView : ViewBase, IView
    {
        private const float POPUP_SHOW_ANIMATION_TIME = 0.3f;
        private const float POPUP_HIDE_ANIMATION_TIME = 0.2f;
        private const float PACK_SHOW_ANIMATION_TIME = 0.25f;
        private const float PACK_SHOW_ANIMATION_STAGGER = 0.08f;

        [field: SerializeField] public RectTransform ContainerTransform { get; private set; } = null!;

        [field: Header("Chrome")]
        [field: SerializeField] public GameObject HeaderContainer { get; private set; } = null!;

        [field: Header("Packs")]
        [field: SerializeField] public CreditsTopUpPackItemView[] PackItems { get; private set; } = null!;
        [field: SerializeField] public GameObject PacksLoadingSpinner { get; private set; } = null!;
        [field: SerializeField] public GameObject PacksErrorContainer { get; private set; } = null!;

        [field: Header("Balance")]
        [field: SerializeField] public GameObject BalanceContainer { get; private set; } = null!;
        [field: SerializeField] public TMP_Text BalanceCreditsText { get; private set; } = null!;
        [field: SerializeField] public TMP_Text BoughtCreditsAmount { get; private set; } = null!;

        [field: Header("Actions")]
        [field: SerializeField] public Button CloseButton { get; private set; } = null!;

        [field: Header("States")]
        [field: SerializeField] public GameObject PackSelectionContainer { get; private set; } = null!;
        [field: SerializeField] public GameObject WaitingForBrowserContainer { get; private set; } = null!;
        [field: SerializeField] public GameObject FailedContainer { get; private set; } = null!;
        [field: SerializeField] public GameObject SuccessContainer { get; private set; } = null!;
        [field: SerializeField] public Image? SuccessPackImage { get; private set; }
        [field: SerializeField] public TMP_Text FailedReasonText { get; private set; } = null!;
        [field: SerializeField] public Button RetryButton { get; private set; } = null!;
        [field: SerializeField] public Button DoneButton { get; private set; } = null!;

        public void AnimatePackItemsPopIn(int count)
        {
            for (var i = 0; i < PackItems.Length && i < count; i++)
            {
                CreditsTopUpPackItemView packItem = PackItems[i];
                Transform packTransform = packItem.transform;

                packTransform.DOKill();
                packTransform.localScale = Vector3.zero;
                packItem.gameObject.SetActive(false);

                packTransform.DOScale(Vector3.one, PACK_SHOW_ANIMATION_TIME)
                             .SetEase(Ease.OutBack)
                             .SetDelay(i * PACK_SHOW_ANIMATION_STAGGER)
                             .OnStart(() => packItem.gameObject.SetActive(true));
            }
        }

        protected override UniTask PlayShowAnimationAsync(CancellationToken ct)
        {
            ContainerTransform.DOKill();
            ContainerTransform.localScale = Vector3.zero;
            return ContainerTransform.DOScale(Vector3.one, POPUP_SHOW_ANIMATION_TIME).SetEase(Ease.OutBack).ToUniTask(cancellationToken: ct);
        }

        protected override UniTask PlayHideAnimationAsync(CancellationToken ct)
        {
            foreach (CreditsTopUpPackItemView packItem in PackItems)
                packItem.transform.DOKill();

            ContainerTransform.DOKill();
            return ContainerTransform.DOScale(Vector3.zero, POPUP_HIDE_ANIMATION_TIME).SetEase(Ease.InBack).ToUniTask(cancellationToken: ct);
        }
    }
}
