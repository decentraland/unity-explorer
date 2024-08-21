using DCL.NotificationsBusController.NotificationTypes;
using UnityEngine;
using Utility;

namespace DCL.Notifications
{
    [CreateAssetMenu(fileName = "NotificationIcons", menuName = "SO/NotificationIcons")]
    public class NotificationIconTypes : ScriptableObject
    {
        [SerializeField] private SerializableKeyValuePair<NotificationType, Sprite>[] notificationIcons;
        [SerializeField] private Sprite defaultIcon;

        public Sprite GetNotificationIcon(NotificationType notificationType)
        {
            foreach (var icon in notificationIcons)
            {
                if (icon.key == notificationType)
                    return icon.value;
            }

            return defaultIcon;
        }
    }
}
