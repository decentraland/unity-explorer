
namespace DCL.NotificationsBus.NotificationTypes
{
    /// <summary>
    /// An internal notification used to let the user know that the scene they are in wrote to the system clipboard,
    /// discarding whatever they had copied before.
    /// It will appear at the top of the screen, and not in the notifications feed.
    /// </summary>
    public class SceneClipboardWriteNotification : NotificationBase
    {
        private const string HEADER_TEXT = "A scene has replaced your clipboard content";

        public override string GetHeader() =>
            HEADER_TEXT;

        public SceneClipboardWriteNotification()
        {
            Type = NotificationType.INTERNAL_SCENE_CLIPBOARD_WRITE;
        }
    }
}
