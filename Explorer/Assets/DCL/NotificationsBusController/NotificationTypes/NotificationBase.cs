using JetBrains.Annotations;
using System;

namespace DCL.NotificationsBusController.NotificationTypes
{
    [Serializable]
    public class NotificationBase : INotification
    {
        public string Id { get; set; }
        public NotificationType Type { get; set; }
        public string Address { get; set; }
        public string Timestamp { get; set; }
        public bool Read { get; set; }

        public virtual string GetHeader() =>
            string.Empty;

        public virtual string GetTitle() =>
            string.Empty;

        public virtual Uri GetThumbnail() =>
            null;
    }
}
