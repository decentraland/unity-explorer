using System;
using DCL.Audio;
using DCL.NotificationsBus.NotificationTypes;
using DCL.UI;
using DCL.Utilities;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DCL.Notifications.NotificationEntry
{
    public class GiftNotificationView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, INotificationView
    {
        private const string GIFT_SENT_FROM_NAME_TEMPLATE = "<color=#{0}>{1} <color=#ECEBED>{2}";

        public event Action<NotificationType, INotification>? NotificationClicked;
        public NotificationType NotificationType { get; set; }
        public INotification Notification { get; set; }
        [field: SerializeField] public Color NormalColor { get; private set; }
        [field: SerializeField] public Color HoveredColor { get; private set; }
        [field: SerializeField] public Image Background { get; private set; }
        [field: SerializeField] public Button MainButton { get; private set; }
        [field: SerializeField] public TMP_Text HeaderText { get; set; }
        [field: SerializeField] public TMP_Text TitleText { get; set; }
        [field: SerializeField] public TMP_Text GiftNameText { get; set; }
        [field: SerializeField] public TMP_Text TimeText { get; set; }
        [field: SerializeField] public Button CloseButton { get; set; }
        [field: SerializeField] public GameObject UnreadImage { get; set; }
        [field: SerializeField] public Image NotificationTypeImage { get; set; }
        [field: SerializeField] public ImageView NotificationImage { get; set; }
        [field: SerializeField] public Image NotificationImageBackground { get; set; }
        [field: SerializeField] public AudioClipConfig RequestNotificationAudio { get; private set; }
        [field: SerializeField] public AudioClipConfig AcceptedNotificationAudio { get; private set; }

        public void PlayRequestNotificationAudio()
        {
            UIAudioEventsBus.Instance.SendPlayAudioEvent(RequestNotificationAudio);
        }

        public void PlayAcceptedNotificationAudio()
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

            // 1. Format the Name Color
            // We get the color based on the name (Gold for guest, etc)
            var userColor = NameColorHelper.GetNameColor(notification.Metadata.Sender.Name);
            string hexColor = ColorUtility.ToHtmlStringRGB(userColor);

            // 2. Set the Title Text (Matches your Figma Design)
            // Output: "PlayerName sent you a Gift!"
            TitleText.text = $"<color=#{hexColor}>{notification.Metadata.Sender.Name}</color> sent you a Gift!";

            // 3. Set the Time
            // Usually TimestampUtilities.GetRelativeTime(notification.Timestamp)
            TimeText.text = "Just now";

            // 4. Set the Icon Background (The Rarity Color)
            // Ensure you have the NftTypeIconSO reference injected or passed in
            // NotificationImageBackground.sprite = rarityBackgroundMapping.GetTypeImage(notification.Metadata.Item.GiftRarity);

            // 5. Set the Image (The Item Thumbnail)
            // NotificationImage.SetImage(notification.Metadata.Item.ImageUrl);
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