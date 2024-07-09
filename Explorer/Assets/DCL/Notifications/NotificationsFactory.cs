using System;
using UnityEngine;

namespace DCL.Notification
{
    public static class NotificationsFactory
    {
        public static INotification CreateNotification(NotificationDTO dto)
        {
            if (Enum.TryParse(typeof(NotificationType), dto.type, true, out object type))
            {
                throw new ArgumentException($"Unsupported notification type: {dto.type}");
            }

            switch (dto.type)
            {
                case "events_started":
                    return new EventStartedNotification()
                    {
                        Id = dto.id,
                        Type = (NotificationType)type,
                        Address = dto.address,
                        Timestamp = dto.timestamp,
                        Read = dto.read,
                        StartedNotificationMetadata = JsonUtility.FromJson<EventStartedNotificationMetadata>(JsonUtility.ToJson(dto.metadata))
                    };
                case "Message":
                default:
                    throw new ArgumentException($"Unsupported notification type: {dto.type}");
            }
        }
    }

}
