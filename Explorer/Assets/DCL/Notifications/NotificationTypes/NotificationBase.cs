using System;

namespace DCL.Notification
{
    [Serializable]
    public class NotificationBase : INotification
    {
        public string Id { get; set; }
        public NotificationType Type { get; set; }
        public string Address { get; set; }
        public string Timestamp { get; set; }
        public bool Read { get; set; }
    }
}
