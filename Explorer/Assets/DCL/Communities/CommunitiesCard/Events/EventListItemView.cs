using DCL.EventsApi;
using DCL.UI;
using DCL.WebRequests;
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
        [SerializeField] private Button interestedButton;
        [SerializeField] private Button offlineShareButton;

        [Header("Event info")]
        [SerializeField] private ImageView eventThumbnailImage;
        [SerializeField] private TMP_Text eventTimeText;
        [SerializeField] private TMP_Text eventNameText;
        [SerializeField] private TMP_Text eventOnlineUsersText;
        [SerializeField] private TMP_Text eventInterestedUsersText;

        public event Action<EventDTO> MainButtonClicked;
        public event Action<EventDTO> JumpInButtonClicked;
        public event Action<EventDTO> InterestedButtonClicked;
        public event Action<EventDTO, Vector2, EventListItemView> ShareButtonClicked;

        private EventDTO? eventData;
        private ImageController imageController;

        private bool canUnHover = true;
        internal bool CanUnHover
        {
            get => canUnHover;
            set
            {
                if (!canUnHover && value)
                {
                    canUnHover = value;
                    OnPointerExit(null);
                }
                canUnHover = value;
            }
        }

        private void Awake()
        {
            mainButton.onClick.AddListener(() => MainButtonClicked?.Invoke(eventData!.Value));
            jumpInButton.onClick.AddListener(() => JumpInButtonClicked?.Invoke(eventData!.Value));
            interestedButton.onClick.AddListener(() => InterestedButtonClicked?.Invoke(eventData!.Value));
            liveShareButton.onClick.AddListener(() => ShareButtonClicked?.Invoke(eventData!.Value, liveShareButton.transform.position, this));
            offlineShareButton.onClick.AddListener(() => ShareButtonClicked?.Invoke(eventData!.Value, offlineShareButton.transform.position, this));
        }

        public void Configure(EventDTO data, IWebRequestController webRequestController)
        {
            eventData = data;
            imageController ??= new ImageController(eventThumbnailImage, webRequestController);

            imageController.RequestImage(data.image);
        }

        public void SubscribeToInteractions(Action<EventDTO> mainAction,
                                            Action<EventDTO> jumpInAction,
                                            Action<EventDTO> interestedAction,
                                            Action<EventDTO, Vector2, EventListItemView> shareAction)
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
            offlineInteractionContainer.SetActive(!eventData!.Value.live);
            liveInteractionContainer.SetActive(eventData!.Value.live);
        }

        public void OnPointerExit(PointerEventData data)
        {
            offlineInteractionContainer.SetActive(false);
            liveInteractionContainer.SetActive(false);
        }
    }
}
