using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.UI
{
    public class ButtonWithSelectableStateView : MonoBehaviour
    {
        [field: SerializeField] public Button Button { get; private set; }
        [field: SerializeField] public Image BackgroundImage { get; private set; }
        [field: SerializeField] public Image IconImage { get; private set; }
        [field: SerializeField] public TMP_Text Text { get; private set; }
        [field: SerializeField] public Color SelectedBackgroundColor { get; private set; }
        [field: SerializeField] public Color UnselectedBackgroundColor { get; private set; }
        [field: SerializeField] public Color SelectedTextColor { get; private set; }
        [field: SerializeField] public Color UnselectedTextColor { get; private set; }

        public void SetSelected(bool selected)
        {
            BackgroundImage.color = selected ? SelectedBackgroundColor : UnselectedBackgroundColor;
            Text.color = selected ? SelectedTextColor : UnselectedTextColor;

            if (IconImage != null)
                IconImage.color = selected ? SelectedTextColor : UnselectedTextColor;
        }
    }
}
