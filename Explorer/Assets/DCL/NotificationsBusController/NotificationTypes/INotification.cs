namespace DCL.NotificationsBusController.NotificationTypes
{
    public interface INotification
    {
        string Id { get; }
        NotificationType Type { get; }
        string Address { get; }
        string Timestamp { get; }
        bool Read { get; set; }

        public string GetHeader();
        public string GetTitle();
        public string GetThumbnail();
    }
}
