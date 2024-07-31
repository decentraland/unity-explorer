using System;

namespace DCL.Notification
{
    [Serializable]
    public class DestinationReachedNotification : NotificationBase
    {
        public override string GetHeader() =>
            "You have arrived to your destination";

    }
}
