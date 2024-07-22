using System;
using System.Collections.Generic;
using UnityEngine;

namespace DCL.Notification.NotificationsBus
{
    public class NotificationsBusController : INotificationsBusController
    {
        public delegate void EventDelegate(params object[] parameters);
        public event Action<INotification> OnNotificationAdded;
        private Dictionary<NotificationType, EventDelegate> notificationSubscribers = new();

        public void AddNotification(INotification notification)
        {
            OnNotificationAdded?.Invoke(notification);
        }

        public void ClickNotification(NotificationType notificationType, params object[] parameters)
        {
            if (notificationSubscribers.TryGetValue(notificationType, out EventDelegate thisEvent))
            {
                thisEvent.Invoke(parameters);
            }
        }

        public void SubscribeToNotificationType(NotificationType desiredType, EventDelegate listener)
        {
            if (notificationSubscribers.TryGetValue(desiredType, out EventDelegate thisEvent))
            {
                thisEvent += listener;
                notificationSubscribers[desiredType] = thisEvent;
            }
            else
            {
                thisEvent += listener;
                notificationSubscribers.Add(desiredType, thisEvent);
            }
        }
    }
}
