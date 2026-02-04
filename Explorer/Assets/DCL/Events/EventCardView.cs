using DCL.EventsApi;
using DCL.PlacesAPIService;
using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.Events
{
    public class EventCardView : MonoBehaviour
    {
        public event Action<EventDTO, PlacesData.PlaceInfo?>? MainButtonClicked;

        [SerializeField] private Button mainButton = null!;
        [SerializeField] private TMP_Text eventText = null!;

        private EventDTO currentEventInfo;
        private PlacesData.PlaceInfo? currentPlaceInfo;

        private void Awake()
        {
            if (mainButton != null)
                mainButton.onClick.AddListener(() => MainButtonClicked?.Invoke(currentEventInfo, currentPlaceInfo));
        }

        private void OnDestroy()
        {
            if (mainButton != null)
                mainButton.onClick.RemoveAllListeners();
        }

        public void Configure(EventDTO eventInfo, PlacesData.PlaceInfo? placeInfo = null)
        {
            currentEventInfo = eventInfo;
            currentPlaceInfo = placeInfo;

            // This is temporal until we fully implement the event card view.
            if (eventText != null)
                eventText.text = $"{eventInfo.name}\n{DateTimeOffset.Parse(eventInfo.next_start_at).LocalDateTime:dd/MM/yyyy HH:mm}";
        }
    }
}
