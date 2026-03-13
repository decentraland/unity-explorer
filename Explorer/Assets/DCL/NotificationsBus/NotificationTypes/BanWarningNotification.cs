namespace DCL.NotificationsBus.NotificationTypes
{
    public class BanWarningNotification : NotificationBase
    {
        private const string NOTIFICATION_HEADER = "Your account has been flagged";
        private const string NOTIFICATION_TITLE = "Further violations will lead to a ban.";

        public override string GetHeader() =>
            NOTIFICATION_HEADER;

        public override string GetTitle() =>
            NOTIFICATION_TITLE;
    }
}
