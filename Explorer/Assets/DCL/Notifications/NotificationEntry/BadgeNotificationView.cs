using DCL.Audio;
using DCL.NotificationsBusController.NotificationTypes;
using DCL.UI;
using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DCL.Notifications.NotificationEntry
{
    public class BadgeNotificationView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
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
        public Button MainButton { get; private set; }

        [field: SerializeField]
        public TMP_Text HeaderText { get; private set; }

        [field: SerializeField]
        public TMP_Text TitleText { get; private set; }

        [field: SerializeField]
        public ImageView NotificationImage { get; private set; }

        [field: SerializeField]
        public AudioClipConfig NotificationAudio { get; private set; }

        private void Start()
        {
            Background.color = NormalColor;
            MainButton.onClick.AddListener(() => NotificationClicked?.Invoke(NotificationType, Notification));
        }

        private void OnDestroy() =>
            MainButton.onClick.RemoveAllListeners();

        public void OnPointerEnter(PointerEventData eventData) =>
            Background.color = HoveredColor;

        public void OnPointerExit(PointerEventData eventData) =>
            Background.color = NormalColor;

        public void PlayNotificationAudio() =>
            UIAudioEventsBus.Instance.SendPlayAudioEvent(NotificationAudio);
    }
}
