using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.MarketplaceCredits.Purchase.TopUp.UI
{
    public class CreditsTopUpPackItemView : MonoBehaviour
    {
        [field: SerializeField] public Button BuyButton { get; private set; } = null!;
        [field: SerializeField] public TMP_Text PriceText { get; private set; } = null!;
        [field: SerializeField] public TMP_Text CreditsText { get; private set; } = null!;
        [field: SerializeField] public GameObject BestValueBadge { get; private set; } = null!;
    }
}
