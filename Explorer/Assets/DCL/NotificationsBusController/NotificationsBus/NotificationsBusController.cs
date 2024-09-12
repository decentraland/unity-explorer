using DCL.NotificationsBusController.NotificationTypes;
using System;
using System.Collections.Generic;
using Utility;

namespace DCL.NotificationsBusController.NotificationsBus
{
    public class NotificationsBusController : INotificationsBusController
    {
        public static readonly IReadOnlyList<NotificationType> NOTIFICATION_TYPES = EnumUtils.Values<NotificationType>();

        public delegate void NotificationClickedDelegate(params object[] parameters);
        public delegate void NotificationReceivedDelegate(INotification notification);

        private readonly Dictionary<NotificationType, NotificationClickedDelegate> notificationClickedSubscribers = new();
        private readonly Dictionary<NotificationType, NotificationReceivedDelegate> notificationReceivedSubscribers = new();

        public void AddNotification(INotification notification)
        {
            if (notificationReceivedSubscribers.TryGetValue(notification.Type, out NotificationReceivedDelegate thisEvent))
                thisEvent.Invoke(notification);
        }

        public void ClickNotification(NotificationType notificationType, params object[] parameters)
        {
            if (notificationClickedSubscribers.TryGetValue(notificationType, out NotificationClickedDelegate thisEvent))
                thisEvent.Invoke(parameters);
        }

        public void SubscribeToNotificationTypeClick(NotificationType desiredType, NotificationClickedDelegate listener)
        {
            notificationClickedSubscribers.TryGetValue(desiredType, out NotificationClickedDelegate thisEvent);
            thisEvent += listener;
            notificationClickedSubscribers[desiredType] = thisEvent;
        }

        public void SubscribeToNotificationTypeReceived(NotificationType desiredType, NotificationReceivedDelegate listener)
        {
            notificationReceivedSubscribers.TryGetValue(desiredType, out NotificationReceivedDelegate thisEvent);
            thisEvent += listener;
            notificationReceivedSubscribers[desiredType] = thisEvent;
        }

        public void SubscribeToAllNotificationTypesClick(NotificationClickedDelegate listener)
        {
            for (var i = 0; i < NOTIFICATION_TYPES.Count; i++)
            {
                NotificationType notificationType = (NotificationType)i;
                notificationClickedSubscribers.TryGetValue(notificationType, out NotificationClickedDelegate thisEvent);
                thisEvent += listener;
                notificationClickedSubscribers[notificationType] = thisEvent;
            }
        }

        public void SubscribeToAllNotificationTypesReceived(NotificationReceivedDelegate listener)
        {
            for (var i = 0; i < NOTIFICATION_TYPES.Count; i++)
            {
                NotificationType notificationType = (NotificationType)i;
                notificationReceivedSubscribers.TryGetValue(notificationType, out NotificationReceivedDelegate thisEvent);
                thisEvent += listener;
                notificationReceivedSubscribers[notificationType] = thisEvent;
            }
        }
    }
}
