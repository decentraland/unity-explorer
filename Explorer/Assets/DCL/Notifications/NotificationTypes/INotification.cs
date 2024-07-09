namespace DCL.Notification
{
    public interface INotification
    {
        string Id { get; }
        NotificationType Type { get; }
        string Address { get; }
        string Timestamp { get; }
        bool Read { get; }
    }
}
