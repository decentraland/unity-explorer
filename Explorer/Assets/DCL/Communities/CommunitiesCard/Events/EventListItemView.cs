using DCL.EventsApi;
using DCL.UI;
using DCL.WebRequests;
using System;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DCL.Communities.CommunitiesCard.Events
{
    public class EventListItemView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        private const string STARTED_EVENT_TIME_FORMAT = "Started {0} {1} ago";
        private const string EVENT_TIME_FORMAT = "ddd, MMM dd @ h:mmtt";
        private const string DAY_STRING = "day";
        private const string HOUR_STRING = "hour";
        private const string MINUTES_STRING = "min";

        [SerializeField] private GameObject offlineInteractionContainer;
        [SerializeField] private GameObject liveInteractionContainer;

        [SerializeField] private Button mainButton;
        [SerializeField] private Button jumpInButton;
        [SerializeField] private Button liveShareButton;
        [SerializeField] private ButtonWithSelectableStateView interestedButton;
        [SerializeField] private Button offlineShareButton;

        [Space(10)]
        [SerializeField] private GameObject liveBadgeContainer;
        [SerializeField] private GameObject interestedContainer;

        [Header("Event info")]
        [SerializeField] private ImageView eventThumbnailImage;
        [SerializeField] private TMP_Text eventTimeText;
        [SerializeField] private TMP_Text eventNameText;
        [SerializeField] private TMP_Text eventOnlineUsersText;
        [SerializeField] private TMP_Text eventInterestedUsersText;

        public event Action<PlaceAndEventDTO> MainButtonClicked;
        public event Action<PlaceAndEventDTO> JumpInButtonClicked;
        public event Action<PlaceAndEventDTO> InterestedButtonClicked;
        public event Action<PlaceAndEventDTO, Vector2> ShareButtonClicked;

        private PlaceAndEventDTO? eventData;
        private ImageController imageController;

        private bool canPlayUnHoverAnimation = true;
        internal bool CanPlayUnHoverAnimation
        {
            get => canPlayUnHoverAnimation;
            set
            {
                if (!canPlayUnHoverAnimation && value)
                {
                    canPlayUnHoverAnimation = value;
                    UnHoverAnimation();
                }
                canPlayUnHoverAnimation = value;
            }
        }

        private void Awake()
        {
            mainButton.onClick.AddListener(() => MainButtonClicked?.Invoke(eventData!.Value));
            jumpInButton.onClick.AddListener(() => JumpInButtonClicked?.Invoke(eventData!.Value));
            interestedButton.Button.onClick.AddListener(() => InterestedButtonClicked?.Invoke(eventData!.Value));
            interestedButton.Button.onClick.AddListener(() => interestedButton.SetSelected(!interestedButton.Selected));
            liveShareButton.onClick.AddListener(() => ShareButtonClicked?.Invoke(eventData!.Value, liveShareButton.transform.position));
            offlineShareButton.onClick.AddListener(() => ShareButtonClicked?.Invoke(eventData!.Value, offlineShareButton.transform.position));
        }

        private static string GetEventTimeText(EventDTO data)
        {
            string schedule = string.Empty;

            if (!DateTime.TryParse(data.start_at, null, DateTimeStyles.RoundtripKind, out DateTime startAt)) return schedule;

            if (data.live)
            {
                TimeSpan elapsed = DateTime.UtcNow - startAt;

                if (elapsed.TotalDays >= 1)
                    schedule = string.Format(STARTED_EVENT_TIME_FORMAT, (int)elapsed.TotalDays, DAY_STRING);
                else if (elapsed.TotalHours >= 1)
                    schedule = string.Format(STARTED_EVENT_TIME_FORMAT, (int)elapsed.TotalHours, HOUR_STRING);
                else
                    schedule = string.Format(STARTED_EVENT_TIME_FORMAT, (int)elapsed.TotalMinutes, MINUTES_STRING);
            }
            else
            {
                DateTime localDateTime = startAt.ToLocalTime();
                schedule = localDateTime.ToString(EVENT_TIME_FORMAT).ToUpper();
            }

            return schedule;
        }

        public void Configure(PlaceAndEventDTO data, IWebRequestController webRequestController)
        {
            eventData = data;
            imageController ??= new ImageController(eventThumbnailImage, webRequestController);

            imageController.RequestImage(data.Event.image);
            eventTimeText.text = GetEventTimeText(data.Event);
            eventNameText.text = data.Event.name;
            UpdateInterestedCounter();
            eventOnlineUsersText.text = data.Place.user_count.ToString();

            UpdateInterestedButtonState();
            liveBadgeContainer.SetActive(data.Event.live);
            interestedContainer.SetActive(data.Event is { live: false, total_attendees: > 0 });
            UnHoverAnimation();
        }

        public void UpdateInterestedButtonState() =>
            interestedButton.SetSelected(eventData!.Value.Event.attending);

        public void UpdateInterestedCounter() =>
            eventInterestedUsersText.text = eventData!.Value.Event.total_attendees.ToString();

        public void SubscribeToInteractions(Action<PlaceAndEventDTO> mainAction,
                                            Action<PlaceAndEventDTO> jumpInAction,
                                            Action<PlaceAndEventDTO> interestedAction,
                                            Action<PlaceAndEventDTO, Vector2> shareAction)
        {
            MainButtonClicked = null;
            JumpInButtonClicked = null;
            InterestedButtonClicked = null;
            ShareButtonClicked = null;

            MainButtonClicked += mainAction;
            JumpInButtonClicked += jumpInAction;
            InterestedButtonClicked += interestedAction;
            ShareButtonClicked += shareAction;
        }

        public void OnPointerEnter(PointerEventData data)
        {
            offlineInteractionContainer.SetActive(!eventData!.Value.Event.live);
            liveInteractionContainer.SetActive(eventData!.Value.Event.live);
        }

        public void OnPointerExit(PointerEventData data)
        {
            if (canPlayUnHoverAnimation)
                UnHoverAnimation();
        }

        private void UnHoverAnimation()
        {
            offlineInteractionContainer.SetActive(false);
            liveInteractionContainer.SetActive(false);
        }
    }
}
