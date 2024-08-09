using UnityEngine;

namespace DCL.UI.Buttons
{
    [RequireComponent(typeof(HoverableButton))]
    public class ShowTooltipOnHoverElement : MonoBehaviour
    {
        [field: SerializeField]
        private GameObject tooltip { get; set; }

        [field: SerializeField]
        private HoverableButton hoverableButton { get; set; }

        private void Awake()
        {
            hoverableButton.OnButtonHover += ShowTooltip;
            hoverableButton.OnButtonUnhover += HideTooltip;
        }

        private void OnDestroy()
        {
            hoverableButton.OnButtonHover -= ShowTooltip;
            hoverableButton.OnButtonUnhover -= HideTooltip;
        }

        private void ShowTooltip()
        {
            SetTooltip(true);
        }

        private void HideTooltip()
        {
            SetTooltip(false);
        }

        private void SetTooltip(bool show)
        {
            tooltip.SetActive(show);
        }
    }
}
