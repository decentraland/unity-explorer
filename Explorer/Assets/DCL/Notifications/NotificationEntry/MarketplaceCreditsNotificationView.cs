using DCL.Audio;
using DCL.NotificationsBusController.NotificationTypes;
using DCL.UI;
using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.Notifications.NotificationEntry
{
    public class MarketplaceCreditsNotificationView : MonoBehaviour
    {
        public event Action<NotificationType, INotification> NotificationClicked;
        public NotificationType NotificationType { get; set; }
        public INotification Notification { get; set; }

        [field: SerializeField]
        public Button MainButton { get; private set; }

        [field: SerializeField]
        public TMP_Text HeaderText { get; private set; }

        [field: SerializeField]
        public TMP_Text TitleText { get; private set; }

        [field: SerializeField]
        public ImageView NotificationImage { get; private set; }

        [field: SerializeField]
        public AudioClipConfig NotificationAudio { get; private set; }

        public void SetHeaderText(string text) =>
            HeaderText.text = text;

        public void SetTitleText(string text) =>
            TitleText.text = text;

        public void SetNotification(NotificationType notificationType, INotification notification)
        {
            NotificationType = notificationType;
            Notification = notification;
        }

        private void Start() =>
            MainButton.onClick.AddListener(OnMainButtonClicked);

        private void OnDestroy() =>
            MainButton.onClick.RemoveAllListeners();

        public void PlayNotificationAudio() =>
            UIAudioEventsBus.Instance.SendPlayAudioEvent(NotificationAudio);

        private void OnMainButtonClicked() =>
            NotificationClicked.Invoke(NotificationType, Notification);
    }
}
