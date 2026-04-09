namespace DCL.NotificationsBus.NotificationTypes
{
    public class BannedNotification : NotificationBase
    {
        private const string NOTIFICATION_HEADER = "You Have Been Banned";
        private const string NOTIFICATION_TITLE = "Contact Support for more details.";

        public override string GetHeader() =>
            NOTIFICATION_HEADER;

        public override string GetTitle() =>
            NOTIFICATION_TITLE;
    }
}
