using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.Shop
{
    /// <summary>
    ///     One row of a shop dropdown list: paints the label of the row holding the current value with <see cref="SelectedColor" />.
    /// </summary>
    public class ShopDropdownItemView : MonoBehaviour
    {
        [field: SerializeField] public Toggle Toggle { get; private set; } = null!;
        [field: SerializeField] public TMP_Text Label { get; private set; } = null!;
        [field: SerializeField] public Color SelectedColor { get; private set; } = new (1f, 0.45490196f, 0.22352941f, 1f);

        // TMP_Dropdown rebuilds the rows on every opening and sets the toggle right after instantiating them, before Start.
        private void Start()
        {
            if (Toggle.isOn)
                Label.color = SelectedColor;
        }
    }
}
