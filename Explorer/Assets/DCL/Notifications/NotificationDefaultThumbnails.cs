using DCL.NotificationsBus.NotificationTypes;
using System;
using UnityEngine;
using Utility;

namespace DCL.Notifications
{
    [CreateAssetMenu(fileName = "NotificationDefaultThumbnails", menuName = "DCL/Various/Notification Default Thumbnails")]
    public class NotificationDefaultThumbnails : ScriptableObject
    {
        [SerializeField] private SerializableKeyValuePair<NotificationType, DefaultNotificationThumbnail>[] notificationIcons;
        [SerializeField] private Sprite defaultIcon;

        public DefaultNotificationThumbnail GetNotificationDefaultThumbnail(NotificationType notificationType)
        {
            foreach (SerializableKeyValuePair<NotificationType, DefaultNotificationThumbnail> icon in notificationIcons)
            {
                if (icon.key == notificationType)
                    return icon.value;
            }

            return new DefaultNotificationThumbnail
            {
                Thumbnail = defaultIcon,
                FitAndCenter = false
            };
        }
    }

    [Serializable]
    public struct DefaultNotificationThumbnail
    {
        public Sprite Thumbnail;
        public bool FitAndCenter;
    }
}
