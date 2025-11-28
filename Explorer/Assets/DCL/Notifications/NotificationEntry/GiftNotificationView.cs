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
        [field: SerializeField] public AudioClipConfig AcceptedNotificationAudio { get; private set; }

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

            // 1. Initial State: Show truncated address
            string shortAddr = notification.Metadata.SenderAddress.Length > 8 
                ? $"{notification.Metadata.SenderAddress.Substring(0, 4)}..." 
                : notification.Metadata.SenderAddress;

            HeaderText.text = $"{shortAddr} sent you a gift!";
            TimeText.text = GiftingTextIds.JustNowMessage;

            // 2. Set Default Styling (Gray background for generic gift icon)
            if (NotificationImageBackground != null) 
                NotificationImageBackground.color = new Color(0.5f, 0.5f, 0.5f, 1f);  
        }
        
        public void UpdateSenderName(string playerName, Color nameColor)
        {
            string hexColor = ColorUtility.ToHtmlStringRGB(nameColor);
            HeaderText.text = string.Format(GiftingTextIds.GiftReceivedTitleFormat, hexColor, playerName);
        }
        
        // Called by Controller (Optional: if we want rarity color in list)
        public void UpdateRarityBackground(Sprite raritySprite)
        {
            if (NotificationImageBackground != null && raritySprite != null)
            {
                NotificationImageBackground.sprite = raritySprite;
                NotificationImageBackground.color = Color.white;
            }
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