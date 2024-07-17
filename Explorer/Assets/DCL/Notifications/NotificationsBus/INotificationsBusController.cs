using System;

namespace DCL.Notification.NotificationsBus
{
    public interface INotificationsBusController
    {
        public event Action<INotification> OnNotificationAdded;

        void AddNotification(INotification notification);

        void ClickNotification(NotificationType notificationType, params object[] parameters);

        void SubscribeToNotificationType(NotificationType desiredType, NotificationsBusController.EventDelegate listener);
    }
}
