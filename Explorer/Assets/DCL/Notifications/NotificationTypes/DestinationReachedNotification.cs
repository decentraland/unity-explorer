using System;

namespace DCL.Notification
{
    [Serializable]
    public class DestinationReachedNotification : NotificationBase
    {
        public override string GetHeader() =>
            "";

        public override string GetTitle() =>
            "You have arrived to your destination";

        public override string GetThumbnail()
        {
            return null;
        }
    }
}
