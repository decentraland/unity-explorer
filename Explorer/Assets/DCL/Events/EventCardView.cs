using DCL.Communities;
using DCL.Communities.EventInfo;
using DCL.EventsApi;
using DCL.PlacesAPIService;
using DCL.UI;
using System;
using System.Collections.Generic;
using System.Threading;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Utility;

namespace DCL.Events
{
    public class EventCardView : MonoBehaviour
    {
        private const string HOST_FORMAT = "By <b>{0}</b>";

        public event Action<EventDTO, PlacesData.PlaceInfo?>? MainButtonClicked;

        [SerializeField] private Button mainButton = null!;
        [SerializeField] private GameObject backgroundNormal = null!;
        [SerializeField] private GameObject backgroundRemindMe = null!;
        [SerializeField] private GameObject backgroundLive = null!;

        [Header("Event Info")]
        [SerializeField] private ImageView eventThumbnail = null!;
        [SerializeField] private TMP_Text eventText = null!;
        [SerializeField] private TMP_Text hostName = null!;
        [SerializeField] private TMP_Text eventDate = null!;
        [SerializeField] private List<GameObject> liveMarks = null!;

        private EventDTO currentEventInfo;
        private PlacesData.PlaceInfo? currentPlaceInfo;
        private CancellationTokenSource loadingThumbnailCts = null!;

        private void Awake()
        {
            if (mainButton != null)
                mainButton.onClick.AddListener(() => MainButtonClicked?.Invoke(currentEventInfo, currentPlaceInfo));
        }

        private void OnDisable() =>
            loadingThumbnailCts.SafeCancelAndDispose();

        private void OnDestroy()
        {
            if (mainButton != null)
                mainButton.onClick.RemoveAllListeners();
        }

        public void Configure(EventDTO eventInfo, ThumbnailLoader thumbnailLoader, PlacesData.PlaceInfo? placeInfo = null)
        {
            currentEventInfo = eventInfo;
            currentPlaceInfo = placeInfo;

            if (backgroundNormal != null)
                backgroundNormal.SetActive(eventInfo is { attending: false, live: false });

            if (backgroundRemindMe != null)
                backgroundRemindMe.SetActive(eventInfo is { attending: true, live: false });

            if (backgroundLive != null)
                backgroundLive.SetActive(eventInfo.live);

            if (eventThumbnail != null)
            {
                loadingThumbnailCts = loadingThumbnailCts.SafeRestart();
                thumbnailLoader.LoadCommunityThumbnailFromUrlAsync(eventInfo.Image, eventThumbnail, null, loadingThumbnailCts.Token, true).Forget();
            }

            if (eventText != null)
                eventText.text = eventInfo.name;

            if (hostName != null)
                hostName.text = string.Format(HOST_FORMAT, eventInfo.user_name);

            if (eventDate != null)
                eventDate.text = EventUtilities.GetEventTimeText(eventInfo, showOnlyHoursFormat: true);

            foreach (GameObject liveMark in liveMarks)
                liveMark.SetActive(eventInfo.live);

            ;
        }
    }
}
