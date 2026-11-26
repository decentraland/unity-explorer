using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.Shop
{
    public class ShopRarityFilterPillView : MonoBehaviour
    {
        [field: SerializeField] public Button Button { get; private set; } = null!;
        [field: SerializeField] public Image Background { get; private set; } = null!;
        [field: SerializeField] public TMP_Text Label { get; private set; } = null!;
    }
}
