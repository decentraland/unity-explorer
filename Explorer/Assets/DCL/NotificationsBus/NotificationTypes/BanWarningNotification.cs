namespace DCL.NotificationsBus.NotificationTypes
{
    public class BanWarningNotification : NotificationBase
    {
        private const string NOTIFICATION_HEADER = "Policy Violation Detected";
        private const string NOTIFICATION_TITLE = "Your account has been flagged. Contact Support for more details.";

        public override string GetHeader() =>
            NOTIFICATION_HEADER;

        public override string GetTitle() =>
            NOTIFICATION_TITLE;
    }
}
