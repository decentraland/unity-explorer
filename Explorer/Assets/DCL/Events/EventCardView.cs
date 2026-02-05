using DCL.Communities;
using DCL.Communities.EventInfo;
using DCL.EventsApi;
using DCL.PlacesAPIService;
using DCL.UI;
using DCL.UI.Controls.Configs;
using MVC;
using System;
using System.Collections.Generic;
using System.Threading;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Utility;

namespace DCL.Events
{
    public abstract class EventCardView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        private const string HOST_FORMAT = "By <b>{0}</b>";

        public event Action<EventDTO, PlacesData.PlaceInfo?, EventCardView>? MainButtonClicked;
        public event Action<EventDTO, EventCardView>? InterestedButtonClicked;
        public event Action<EventDTO>? AddToCalendarButtonClicked;
        public event Action<EventDTO>? JumpInButtonClicked;
        public event Action<EventDTO>? EventShareButtonClicked;
        public event Action<EventDTO>? EventCopyLinkButtonClicked;

        [Header("Card Configuration")]
        [SerializeField] private GameObject backgroundNormal = null!;
        [SerializeField] private GameObject backgroundRemindMe = null!;
        [SerializeField] private GameObject backgroundLive = null!;
        [SerializeField] private Button mainButton = null!;
        [SerializeField] private ButtonWithSelectableStateView interestedButton = null!;
        [SerializeField] private Button addToCalendarButton = null!;
        [SerializeField] private Button jumpInButton = null!;
        [SerializeField] private Button shareButton = null!;
        [SerializeField] private EventContextMenuConfiguration contextMenuSettings = null!;

        [Header("Event Info")]
        [SerializeField] private ImageView eventThumbnail = null!;
        [SerializeField] private TMP_Text eventText = null!;
        [SerializeField] private TMP_Text hostName = null!;
        [SerializeField] private TMP_Text eventDate = null!;
        [SerializeField] private List<GameObject> liveMarks = null!;
        [SerializeField] private TMP_Text onlineMembersText = null!;
        [SerializeField] private GameObject onlineMembersContainer = null!;

        private EventDTO currentEventInfo;
        private PlacesData.PlaceInfo? currentPlaceInfo;
        private GenericContextMenu? contextMenu;

        private CancellationTokenSource? loadingThumbnailCts;
        private CancellationTokenSource? openContextMenuCts;

        private bool canPlayUnHoverAnimation = true;
        private bool canPlayUnHoverAnimationProp
        {
            get => canPlayUnHoverAnimation;
            set
            {
                if (!canPlayUnHoverAnimation && value)
                {
                    canPlayUnHoverAnimation = value;
                    PlayHoverExitAnimation();
                }
                canPlayUnHoverAnimation = value;
            }
        }

        protected virtual void Awake()
        {
            if (mainButton != null)
                mainButton.onClick.AddListener(() => MainButtonClicked?.Invoke(currentEventInfo, currentPlaceInfo, this));

            if (interestedButton != null)
                interestedButton.Button.onClick.AddListener(() => InterestedButtonClicked?.Invoke(currentEventInfo, this));

            if (addToCalendarButton != null)
                addToCalendarButton.onClick.AddListener(() => AddToCalendarButtonClicked?.Invoke(currentEventInfo));

            if (jumpInButton != null)
                jumpInButton.onClick.AddListener(() => JumpInButtonClicked?.Invoke(currentEventInfo));

            if (shareButton != null)
                shareButton.onClick.AddListener(() => OpenContextMenu(shareButton.transform.position));

            if (contextMenuSettings != null)
            {
                contextMenu = new GenericContextMenu(contextMenuSettings.ContextMenuWidth, verticalLayoutPadding: contextMenuSettings.VerticalPadding,
                                  elementsSpacing: contextMenuSettings.ElementsSpacing,
                                  offsetFromTarget: contextMenuSettings.OffsetFromTarget)
                             .AddControl(new ButtonContextMenuControlSettings(contextMenuSettings.ShareText, contextMenuSettings.ShareSprite, () => EventShareButtonClicked?.Invoke(currentEventInfo)))
                             .AddControl(new ButtonContextMenuControlSettings(contextMenuSettings.CopyLinkText, contextMenuSettings.CopyLinkSprite, () => EventCopyLinkButtonClicked?.Invoke(currentEventInfo)));
            }
        }

        private void OnEnable() =>
            PlayHoverExitAnimation(true);

        private void OnDisable()
        {
            loadingThumbnailCts.SafeCancelAndDispose();
            openContextMenuCts.SafeCancelAndDispose();
        }

        private void OnDestroy()
        {
            if (mainButton != null)
                mainButton.onClick.RemoveAllListeners();

            if (interestedButton != null)
                interestedButton.Button.onClick.RemoveAllListeners();

            if (addToCalendarButton != null)
                addToCalendarButton.onClick.RemoveAllListeners();

            if (jumpInButton != null)
                jumpInButton.onClick.RemoveAllListeners();

            if (shareButton != null)
                shareButton.onClick.RemoveAllListeners();
        }

        public void Configure(EventDTO eventInfo, ThumbnailLoader thumbnailLoader, PlacesData.PlaceInfo? placeInfo = null)
        {
            currentEventInfo = eventInfo;
            currentPlaceInfo = placeInfo;

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

            int onlineMembers = eventInfo.connected_addresses?.Length ?? 0;
            if (onlineMembersText != null) onlineMembersText.text = $"{onlineMembers}";
            if (onlineMembersContainer != null) onlineMembersContainer.SetActive(onlineMembers > 0 && eventInfo.live);

            UpdateInterestedButtonState(currentEventInfo.Attending);
            UpdateVisuals();
        }

        public void UpdateInterestedButtonState(bool isInterested)
        {
            currentEventInfo.attending = isInterested;

            if (interestedButton == null)
                return;

            interestedButton.SetSelected(isInterested);
        }

        public void UpdateVisuals()
        {
            if (backgroundNormal != null)
                backgroundNormal.SetActive(currentEventInfo is { attending: false, live: false });

            if (backgroundRemindMe != null)
                backgroundRemindMe.SetActive(currentEventInfo is { attending: true, live: false });

            if (backgroundLive != null)
                backgroundLive.SetActive(currentEventInfo.live);

            if (interestedButton != null)
                interestedButton.gameObject.SetActive(!currentEventInfo.live);

            if (addToCalendarButton != null)
                addToCalendarButton.gameObject.SetActive(!currentEventInfo.live);

            if (jumpInButton != null)
                jumpInButton.gameObject.SetActive(currentEventInfo.live);
        }

        public void OnPointerEnter(PointerEventData eventData) =>
            PlayHoverAnimation();

        public void OnPointerExit(PointerEventData eventData)
        {
            if (canPlayUnHoverAnimationProp)
                PlayHoverExitAnimation();
        }

        private void OpenContextMenu(Vector2 position)
        {
            canPlayUnHoverAnimationProp = false;
            openContextMenuCts = openContextMenuCts.SafeRestart();
            ViewDependencies.ContextMenuOpener.OpenContextMenu(new GenericContextMenuParameter(contextMenu!, position, actionOnHide: () => canPlayUnHoverAnimationProp = true), openContextMenuCts.Token);
        }

        protected abstract void PlayHoverAnimation();

        protected abstract void PlayHoverExitAnimation(bool instant = false);
    }
}
