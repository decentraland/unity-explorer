namespace DCL.NotificationsBus.NotificationTypes
{
    public class BanWarningNotification : NotificationBase
    {
        private const string NOTIFICATION_HEADER = "This is a warning";
        private const string NOTIFICATION_TITLE = "Your account has been flagged for a policy violation. Further violations will lead to a ban. Please contact support for more information.";

        public override string GetHeader() =>
            NOTIFICATION_HEADER;

        public override string GetTitle() =>
            NOTIFICATION_TITLE;
    }
}
