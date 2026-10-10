
namespace DCL.NotificationsBus.NotificationTypes
{
    /// <summary>
    /// An internal notification used to let the user know that a request to the server failed.
    /// It will appear at the top of the screen, and not in the notifications feed.
    /// </summary>
    public class ServerErrorNotification : NotificationBase
    {
        private readonly string headerText;

        public override string GetHeader() =>
            headerText;

        public ServerErrorNotification(string text)
        {
            headerText = text;
            Type = NotificationType.INTERNAL_SERVER_ERROR;
        }
    }
}
