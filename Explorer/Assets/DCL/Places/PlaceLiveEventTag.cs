using DCL.EventsApi;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace DCL.Places
{
    public class PlaceLiveEventTag : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField] private GameObject liveEventTooltip = null!;
        [SerializeField] private TMP_Text liveEventText = null!;

        private void OnEnable() =>
            liveEventTooltip.SetActive(false);

        public void Configure(EventDTO eventInfo) =>
            liveEventText.text = eventInfo.name;

        public void OnPointerEnter(PointerEventData eventData) =>
            liveEventTooltip.SetActive(true);

        public void OnPointerExit(PointerEventData eventData) =>
            liveEventTooltip.SetActive(false);
    }
}
