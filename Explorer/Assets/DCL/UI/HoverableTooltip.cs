using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace DCL.UI
{
    public class HoverableTooltip : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField] private GameObject tooltip = null!;
        [SerializeField] private TMP_Text text = null!;

        private void OnEnable() =>
            tooltip.SetActive(false);

        public void Configure(string tooltipText) =>
            text.text = tooltipText;

        public void OnPointerEnter(PointerEventData _) =>
            tooltip.SetActive(true);

        public void OnPointerExit(PointerEventData _) =>
            tooltip.SetActive(false);
    }
}
