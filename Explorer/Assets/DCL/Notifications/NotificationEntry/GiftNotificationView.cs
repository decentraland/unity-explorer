using System;
using DCL.Audio;
using DCL.Backpack.Gifting;
using DCL.NotificationsBus.NotificationTypes;
using DCL.UI;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DCL.Notifications.NotificationEntry
{
    public class GiftNotificationView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, INotificationView
    {
        public event Action<NotificationType, INotification>? NotificationClicked;
        public NotificationType NotificationType { get; set; }
        public INotification Notification { get; set; } = null!;
        [field: SerializeField] public Color NormalColor { get; private set; }
        [field: SerializeField] public Color HoveredColor { get; private set; }
        [field: SerializeField] public Image Background { get; private set; } = null!;
        [field: SerializeField] public Button MainButton { get; private set; } = null!;
        [field: SerializeField] public TMP_Text HeaderText { get; set; } = null!;
        [field: SerializeField] public TMP_Text TitleText { get; set; } = null!;
        [field: SerializeField] public TMP_Text GiftNameText { get; set; } = null!;
        [field: SerializeField] public TMP_Text TimeText { get; set; } = null!;
        [field: SerializeField] public Button CloseButton { get; set; } = null!;
        [field: SerializeField] public GameObject UnreadImage { get; set; } = null!;
        [field: SerializeField] public Image NotificationTypeImage { get; set; } = null!;
        [field: SerializeField] public ImageView NotificationImage { get; set; } = null!;
        [field: SerializeField] public Image NotificationImageBackground { get; set; } = null!;
        [field: SerializeField] public AudioClipConfig AcceptedNotificationAudio { get; private set; } = null!;

        private void PlayAcceptedNotificationAudio()
        {
            UIAudioEventsBus.Instance.SendPlayAudioEvent(AcceptedNotificationAudio);
        }

        private void Start()
        {
            Background.color = NormalColor;

            MainButton.onClick.RemoveAllListeners();
            MainButton.onClick.AddListener(OnPointerClick);
            
        }

        public void Configure(GiftReceivedNotification notification)
        {
            Notification = notification;
            NotificationType = notification.Type;

            string shortAddr = notification.Metadata.SenderAddress.Length > 8 
                ? $"{notification.Metadata.SenderAddress.Substring(0, 4)}..." 
                : notification.Metadata.SenderAddress;

            HeaderText.text = string.Format(GiftingTextIds.GIFT_RECEIVED_SENDER_TITLE_FORMAT, shortAddr);
        }
        
        public void UpdateSenderName(string playerName, Color nameColor)
        {
            string hexColor = ColorUtility.ToHtmlStringRGB(nameColor);
            HeaderText.text = string.Format(GiftingTextIds.GIFT_RECEIVED_TITLE_FORMAT, hexColor, playerName);
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