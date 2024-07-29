using System;

namespace DCL.Notification.NotificationsBus
{
    public interface INotificationsBusController
    {
        void AddNotification(INotification notification);
        void ClickNotification(NotificationType notificationType, params object[] parameters);
        void SubscribeToNotificationTypeClick(NotificationType desiredType, NotificationsBusController.NotificationClickedDelegate listener);
        void SubscribeToNotificationTypeReceived(NotificationType desiredType, NotificationsBusController.NotificationReceivedDelegate listener);
    }
}
