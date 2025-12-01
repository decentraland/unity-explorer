using DCL.Audio;
using DCL.NotificationsBus.NotificationTypes;
using DCL.UI;
using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DCL.Notifications.NotificationEntry
{
    public class GiftToastView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        public event Action<NotificationType, INotification> NotificationClicked;
        public NotificationType NotificationType { get; set; }
        public INotification Notification { get; set; }

        [field: SerializeField] public Image Background { get; private set; }
        [field: SerializeField] public Button MainButton { get; private set; }
        [field: SerializeField] public TMP_Text TitleText { get; private set; }
        [field: SerializeField] public ImageView NotificationImage { get; private set; }
        [field: SerializeField] public AudioClipConfig NotificationAudio { get; private set; }

        [field: SerializeField] public Color NormalColor { get; private set; } = Color.white;
        [field: SerializeField] public Color HoveredColor { get; private set; } = new (0.9f, 0.9f, 0.9f);

        private void Start()
        {
            if (Background != null) Background.color = NormalColor;
            MainButton.onClick.AddListener(() => NotificationClicked?.Invoke(NotificationType, Notification));
        }

        private void OnDestroy()
        {
            MainButton.onClick.RemoveAllListeners();
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (Background != null) Background.color = HoveredColor;
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (Background != null) Background.color = NormalColor;
        }

        public void PlayNotificationAudio()
        {
            UIAudioEventsBus.Instance.SendPlayAudioEvent(NotificationAudio);
        }

        public void Configure(GiftReceivedNotification notification)
        {
            Notification = notification;
            NotificationType = notification.Type;

            string shortAddr = notification.Metadata.SenderAddress.Length > 8 
                ? notification.Metadata.SenderAddress.Substring(0, 6) + "..."
                : notification.Metadata.SenderAddress;

            TitleText.text = $"{shortAddr} sent you something!";
        }

        public void UpdateSenderName(string name, Color nameColor)
        {
            string hexColor = ColorUtility.ToHtmlStringRGB(nameColor);
            TitleText.text = $"<color=#{hexColor}><b>{name}</b></color> sent you something!";
        }
    }
}