
namespace DCL.NotificationsBus.NotificationTypes
{
    /// <summary>
    /// An internal notification used to let the user know that an operation was successful.
    /// It will appear at the top of the screen, and not in the notifications feed.
    /// </summary>
    public class DefaultSuccessNotification : NotificationBase
    {
        private readonly string headerText;

        public override string GetHeader() =>
            headerText;

        public DefaultSuccessNotification(string text)
        {
            headerText = text;
            Type = NotificationType.INTERNAL_DEFAULT_SUCCESS;
        }
    }
}
