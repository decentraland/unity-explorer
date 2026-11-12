using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.Shop
{
    /// <summary>
    ///     The rarity tag look shared by the item cards and the item panel: label in the rarity colour over a translucent fill of the same colour.
    /// </summary>
    public static class ShopRarityChip
    {
        private const float BACKGROUND_ALPHA = 0.2f;

        public static void Apply(Image background, TMP_Text label, Color rarityColor)
        {
            rarityColor.a = 1f;
            label.color = rarityColor;

            rarityColor.a = BACKGROUND_ALPHA;
            background.color = rarityColor;
        }
    }
}
