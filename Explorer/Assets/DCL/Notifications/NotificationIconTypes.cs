using DCL.NotificationsBusController.NotificationTypes;
using UnityEngine;
using Utility;

namespace DCL.Notifications
{
    [CreateAssetMenu(fileName = "NotificationIcons", menuName = "DCL/Various/Notification Icons")]
    public class NotificationIconTypes : ScriptableObject
    {
        [SerializeField] private SerializableKeyValuePair<NotificationType, Sprite>[] notificationIcons;
        [SerializeField] private Sprite defaultIcon;
        [SerializeField] private SerializableKeyValuePair<NotificationType, Sprite>[] notificationIconBackgrounds;
        [SerializeField] private Sprite defaultIconBackground;
        [SerializeField] private Color defaultIconBackgroundColor;

        public Sprite GetNotificationIcon(NotificationType notificationType)
        {
            foreach (SerializableKeyValuePair<NotificationType, Sprite> icon in notificationIcons)
            {
                if (icon.key == notificationType)
                    return icon.value;
            }

            return defaultIcon;
        }

        public (Sprite? backgroundSprite, Color backgroundColor) GetNotificationIconBackground(NotificationType notificationType)
        {
            foreach (SerializableKeyValuePair<NotificationType, Sprite> background in notificationIconBackgrounds)
            {
                if (background.key == notificationType)
                    return (background.value, Color.white);
            }

            return (defaultIconBackground, defaultIconBackgroundColor);
        }
    }
}
