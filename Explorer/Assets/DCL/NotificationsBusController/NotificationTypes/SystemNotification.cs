namespace DCL.Notification
{
    public class SystemNotification : NotificationBase
    {
        public string Title { get; set; }

        public override string GetHeader() =>
            Title;

    }
}
