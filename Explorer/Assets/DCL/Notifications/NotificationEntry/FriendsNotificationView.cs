using DCL.Audio;
using DCL.NotificationsBusController.NotificationTypes;
using DCL.UI;
using DCL.Profiles.Helpers;
using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DCL.Notifications.NotificationEntry
{
    public class FriendsNotificationView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, INotificationView
    {
        private const string FRIEND_REQUEST_CLAIMED_NAME_TEMPLATE = "<color=#{0}>{1} <color=#ECEBED>{2}";
        private const string FRIEND_REQUEST_UNCLAIMED_NAME_TEMPLATE = "<color=#{0}>{1}<color=#A09BA8>#{2} <color=#ECEBED>{3}";

        public event Action<NotificationType, INotification> NotificationClicked;
        public NotificationType NotificationType { get; set; }
        public INotification Notification { get; set; }

        [field: SerializeField]
        public Color NormalColor { get; private set; }

        [field: SerializeField]
        public Color HoveredColor { get; private set; }

        [field: SerializeField]
        public Image Background { get; private set; }

        [field: SerializeField]
        public GameObject UnreadImage { get; set; }

        [field: SerializeField]
        public Button MainButton { get; private set; }

        [field: SerializeField]
        public Button CloseButton { get; set; }

        [field: SerializeField]
        public TMP_Text HeaderText { get; set; }

        [field: SerializeField]
        public TMP_Text TitleText { get; set; }

        [field: SerializeField]
        public TMP_Text TimeText { get; set; }

        [field: SerializeField]
        public ImageView NotificationImage { get; set; }

        [field: SerializeField]
        public Image NotificationImageBackground { get; set; }

        [field: SerializeField]
        public Image NotificationTypeImage { get; set; }

        [field: SerializeField]
        public AudioClipConfig RequestNotificationAudio { get; private set; }

        [field: SerializeField]
        public AudioClipConfig AcceptedNotificationAudio { get; private set; }

        public void PlayRequestNotificationAudio() =>
            UIAudioEventsBus.Instance.SendPlayAudioEvent(RequestNotificationAudio);

        public void PlayAcceptedNotificationAudio() =>
            UIAudioEventsBus.Instance.SendPlayAudioEvent(AcceptedNotificationAudio);

        public void ConfigureFromAcceptedNotificationData(FriendRequestAcceptedNotification notification)
        {
            Color userColor = ProfileNameColorHelper.GetNameColor(notification.Metadata.Sender.Name);
            SetTitleText(notification, notification.Metadata.Sender, userColor);
            NotificationImageBackground.color = userColor;
            Notification = notification;
            NotificationType = notification.Type;
        }

        public void ConfigureFromReceivedNotificationData(FriendRequestReceivedNotification notification)
        {
            Color userColor = ProfileNameColorHelper.GetNameColor(notification.Metadata.Sender.Name);
            SetTitleText(notification, notification.Metadata.Sender, userColor);
            NotificationImageBackground.color = userColor;
            Notification = notification;
            NotificationType = notification.Type;
        }

        private void SetTitleText(NotificationBase notification, FriendRequestProfile sender, Color userColor)
        {
            TitleText.SetText(sender.HasClaimedName
                ? string.Format(FRIEND_REQUEST_CLAIMED_NAME_TEMPLATE, ColorUtility.ToHtmlStringRGB(userColor), sender.Name, notification.GetTitle())
                : string.Format(FRIEND_REQUEST_UNCLAIMED_NAME_TEMPLATE, ColorUtility.ToHtmlStringRGB(userColor), sender.Name, sender.Address[^4..], notification.GetTitle()));
        }

        private void Start()
        {
            Background.color = NormalColor;

            MainButton.onClick.RemoveAllListeners();
            MainButton.onClick.AddListener(OnPointerClick);
        }

        private void OnPointerClick()
        {
            NotificationClicked?.Invoke(NotificationType, Notification);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            Background.color = HoveredColor;
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            Background.color = NormalColor;
        }
    }
}
