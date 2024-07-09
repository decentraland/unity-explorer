using System;

namespace DCL.Notification
{
    [Serializable]
    public class EventStartedNotification : NotificationBase
    {
        public EventStartedNotificationMetadata StartedNotificationMetadata { get; set; }
    }

    [Serializable]
    public struct EventStartedNotificationMetadata
    {
        public string link;
        public string name;
        public string image;
        public string title;
        public string description;
    }
}
