namespace DCL.NotificationsBus.NotificationTypes
{
    public class BanLiftedNotification : NotificationBase
    {
        private const string NOTIFICATION_HEADER = "Your Access Has Been Restored";
        private const string NOTIFICATION_TITLE = "Your ban has been lifted, and your account is now active again.";

        public override string GetHeader() =>
            NOTIFICATION_HEADER;

        public override string GetTitle() =>
            NOTIFICATION_TITLE;
    }
}
