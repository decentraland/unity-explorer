using DCL.CharacterPreview;
using MVC;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.MarketplaceCredits.Purchase.UI
{
    public class CreditPurchaseModalView : ViewBase, IView
    {
        [field: SerializeField] public RectTransform ContainerTransform { get; private set; } = null!;

        [field: Header("Item card")]
        [field: SerializeField] public GameObject Item { get; private set; } = null!;
        [field: SerializeField] public Image ItemThumbnail { get; private set; } = null!;
        [field: SerializeField] public Image ItemBackground { get; private set; } = null!;
        [field: SerializeField] public Image ItemCategory { get; private set; } = null!;
        [field: SerializeField] public Image ItemCategoryBackground { get; private set; } = null!;
        [field: SerializeField] public TMP_Text ItemName { get; private set; } = null!;
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
        [field: SerializeField] public Button TryOnButton { get; private set; } = null!;
        [field: SerializeField] public GameObject TryOnPanel { get; private set; } = null!;
        [field: SerializeField] public Button TryOnCloseButton { get; private set; } = null!;
        [field: SerializeField] public Button TryOnReplayEmoteButton { get; private set; } = null!;
        [field: SerializeField] public CharacterPreviewView TryOnCharacterPreviewView { get; private set; } = null!;
    }
}
