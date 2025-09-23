using System;

namespace DCL.NotificationsBus.NotificationTypes
{
    [Serializable]
    public class DestinationReachedNotification : NotificationBase
    {
        public override string GetHeader() =>
            "You have arrived to your destination";

    }
}
