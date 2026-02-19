using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace DCL.Places
{
    public class PlaceCardTagWithTooltip : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField] private GameObject tooltip = null!;
        [SerializeField] private TMP_Text text = null!;

        private void OnEnable() =>
            tooltip.SetActive(false);

        public void Configure(string tooltipText) =>
            text.text = tooltipText;

        public void OnPointerEnter(PointerEventData eventData) =>
            tooltip.SetActive(true);

        public void OnPointerExit(PointerEventData eventData) =>
            tooltip.SetActive(false);
    }
}
