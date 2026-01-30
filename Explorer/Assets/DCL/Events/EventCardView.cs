using DCL.EventsApi;
using System;
using TMPro;
using UnityEngine;

namespace DCL.Events
{
    public class EventCardView : MonoBehaviour
    {
        [SerializeField] private TMP_Text eventText = null!;

        public void Configure(EventDTO eventInfo)
        {
            // This is temporal until we fully implement the event card view.
            if (eventText != null)
                eventText.text = $"{eventInfo.name}\n{DateTimeOffset.Parse(eventInfo.next_start_at).LocalDateTime:dd/MM/yyyy HH:mm}";
        }
    }
}
