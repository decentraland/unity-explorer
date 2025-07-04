using DCL.Communities.EventInfo;
using DCL.UI;
using DCL.Utilities;
using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DCL.Communities.CommunitiesCard.Events
{
    public class EventListItemView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField] private GameObject offlineInteractionContainer;
        [SerializeField] private GameObject liveInteractionContainer;

        [SerializeField] private Button mainButton;
        [SerializeField] private Button jumpInButton;
        [SerializeField] private Button liveShareButton;
        [SerializeField] private ButtonWithSelectableStateView interestedButton;
        [SerializeField] private Button offlineShareButton;

        [Space(10)]
        [SerializeField] private GameObject liveBadgeContainer;
        [SerializeField] private GameObject liveBadgePlayerIcon;
        [SerializeField] private GameObject interestedContainer;

        [Header("Event info")]
        [SerializeField] private ImageView eventThumbnailImage;
        [SerializeField] private TMP_Text eventTimeText;
        [SerializeField] private TMP_Text eventNameText;
        [SerializeField] private TMP_Text eventOnlineUsersText;
        [SerializeField] private TMP_Text eventInterestedUsersText;

        public event Action<PlaceAndEventDTO>? MainButtonClicked;
        public event Action<PlaceAndEventDTO>? JumpInButtonClicked;
        public event Action<PlaceAndEventDTO>? InterestedButtonClicked;
        public event Action<PlaceAndEventDTO, Vector2>? ShareButtonClicked;

        private PlaceAndEventDTO? eventData;
        private ImageController? imageController;

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

        public void Configure(PlaceAndEventDTO data, ObjectProxy<ISpriteCache> spriteCache)
        {
            eventData = data;
            imageController ??= new ImageController(eventThumbnailImage, spriteCache);

            imageController.RequestImage(data.Event.image);
            eventTimeText.text = EventUtilities.GetEventTimeText(data.Event);
            eventNameText.text = data.Event.name;
            UpdateInterestedCounter();
            eventOnlineUsersText.text = data.Place.user_count.ToString();

            UpdateInterestedButtonState();
            liveBadgeContainer.SetActive(data.Event.live);

            bool showLiveOnlineUsers = data.Event.live && data.Place.user_count > 0;
            liveBadgePlayerIcon.SetActive(showLiveOnlineUsers);
            eventOnlineUsersText.gameObject.SetActive(showLiveOnlineUsers);

            UnHoverAnimation();
        }

        public void UpdateInterestedButtonState() =>
            interestedButton.SetSelected(eventData!.Value.Event.attending);

        public void UpdateInterestedCounter()
        {
            eventInterestedUsersText.text = eventData!.Value.Event.total_attendees.ToString();
            interestedContainer.SetActive(eventData!.Value.Event is { live: false, total_attendees: > 0 });
        }

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
