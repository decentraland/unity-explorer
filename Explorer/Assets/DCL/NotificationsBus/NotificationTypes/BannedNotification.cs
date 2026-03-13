namespace DCL.NotificationsBus.NotificationTypes
{
    public class BannedNotification : NotificationBase
    {
        private const string NOTIFICATION_HEADER = "You have been banned";
        private const string NOTIFICATION_TITLE = "Please contact support team for more information.";

        public override string GetHeader() =>
            NOTIFICATION_HEADER;

        public override string GetTitle() =>
            NOTIFICATION_TITLE;
    }
}
