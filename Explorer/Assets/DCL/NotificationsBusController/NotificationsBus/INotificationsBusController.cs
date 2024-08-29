using DCL.NotificationsBusController.NotificationTypes;

namespace DCL.NotificationsBusController.NotificationsBus
{
    public interface INotificationsBusController
    {
        void AddNotification(INotification notification);
        void ClickNotification(NotificationType notificationType, params object[] parameters);
        void SubscribeToNotificationTypeClick(NotificationType desiredType, NotificationsBusController.NotificationClickedDelegate listener);
        void SubscribeToNotificationTypeReceived(NotificationType desiredType, NotificationsBusController.NotificationReceivedDelegate listener);
        void SubscribeToAllNotificationTypesClick(NotificationsBusController.NotificationClickedDelegate listener);
        void SubscribeToAllNotificationTypesReceived(NotificationsBusController.NotificationReceivedDelegate listener);
    }
}
