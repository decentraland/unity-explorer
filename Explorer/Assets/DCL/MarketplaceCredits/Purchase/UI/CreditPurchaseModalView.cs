using Cysharp.Threading.Tasks;
using DCL.CharacterPreview;
using DG.Tweening;
using MVC;
using System.Threading;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.MarketplaceCredits.Purchase.UI
{
    public class CreditPurchaseModalView : ViewBase, IView
    {
        private const float POPUP_SHOW_ANIMATION_TIME = 0.3f;
        private const float POPUP_HIDE_ANIMATION_TIME = 0.2f;

        [field: SerializeField] public RectTransform ContainerTransform { get; private set; } = null!;

        [field: Header("Item card")]
        [field: SerializeField] public GameObject Item { get; private set; } = null!;
        [field: SerializeField] public Image RarityBackgroundFull { get; private set; } = null!;
        [field: SerializeField] public Image ItemThumbnail { get; private set; } = null!;
        [field: SerializeField] public Image ItemBackground { get; private set; } = null!;
        [field: SerializeField] public Image ItemCategory { get; private set; } = null!;
        [field: SerializeField] public Image ItemCategoryBackground { get; private set; } = null!;
        [field: SerializeField] public TMP_Text ItemName { get; private set; } = null!;
        [field: SerializeField] public TMP_Text CreatorName { get; private set; } = null!;
        [field: SerializeField] public TMP_Text RarityLabel { get; private set; } = null!;
        [field: SerializeField] public Image RarityBackground { get; private set; } = null!;

        [field: Header("Completed Item card")]
        [field: SerializeField] public Image ItemThumbnailCompleted { get; private set; } = null!;
        [field: SerializeField] public Image ItemBackgroundCompleted { get; private set; } = null!;
        [field: SerializeField] public Image ItemCategoryCompleted { get; private set; } = null!;
        [field: SerializeField] public Image ItemCategoryBackgroundCompleted { get; private set; } = null!;

        [field: Header("Price and balance")]
        [field: SerializeField] public TMP_Text PriceCreditsText { get; private set; } = null!;
        [field: SerializeField] public GameObject PriceLoadingSpinner { get; private set; } = null!;
        [field: SerializeField] public TMP_Text BalanceCreditsText { get; private set; } = null!;
        [field: SerializeField] public TMP_Text CannotAffortText { get; private set; } = null!;
        [field: SerializeField] public GameObject BalanceLoadingSpinner { get; private set; } = null!;
        [field: SerializeField] public GameObject InsufficientCreditsContainer { get; private set; } = null!;
        [field: SerializeField] public Button GetCreditsButton { get; private set; } = null!;

        [field: Header("Actions")]
        [field: SerializeField] public Button ConfirmButton { get; private set; } = null!;
        [field: SerializeField] public Button CancelButton { get; private set; } = null!;
        [field: SerializeField] public Button InsufficientCancelButton { get; private set; } = null!;
        [field: SerializeField] public Button CloseButton { get; private set; } = null!;
        [field: SerializeField] public Button CloseBackground { get; private set; } = null!;

        [field: Header("States")]
        [field: SerializeField] public GameObject ConfirmStateContainer { get; private set; } = null!;
        [field: SerializeField] public GameObject ProgressStateContainer { get; private set; } = null!;
        [field: SerializeField] public TMP_Text ProgressStatusText { get; private set; } = null!;
        [field: SerializeField] public GameObject SuccessStateContainer { get; private set; } = null!;
        [field: SerializeField] public GameObject FailedStateContainer { get; private set; } = null!;
        [field: SerializeField] public TMP_Text FailedReasonText { get; private set; } = null!;
        [field: SerializeField] public Button RetryButton { get; private set; } = null!;
        [field: SerializeField] public Button OpenMarketplaceButton { get; private set; } = null!;
        [field: SerializeField] public Button ToBackpackButton { get; private set; } = null!;

        [field: Header("Try on")]
        [field: SerializeField] public Button TryOnEmoteToggleButton { get; private set; } = null!;
        [field: SerializeField] public TMP_Text TryOnEmoteToggleButtonText { get; private set; } = null!;
        [field: SerializeField] public CharacterPreviewView TryOnCharacterPreviewView { get; private set; } = null!;

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
