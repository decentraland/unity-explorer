using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace DCL.UI
{
    public class HoverableTooltip : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField] private GameObject tooltip = null!;
        [SerializeField] private TMP_Text text = null!;

        private bool isHoverActive = true;

        private void OnEnable() =>
            tooltip.SetActive(false);

        public void Configure(string tooltipText) =>
            text.text = tooltipText;

        public void SetHoverActive(bool isActive) =>
            isHoverActive = isActive;

        public void OnPointerEnter(PointerEventData _)
        {
            if (!isHoverActive)
                return;

            tooltip.SetActive(true);
        }

        public void OnPointerExit(PointerEventData _) =>
            tooltip.SetActive(false);
    }
}
