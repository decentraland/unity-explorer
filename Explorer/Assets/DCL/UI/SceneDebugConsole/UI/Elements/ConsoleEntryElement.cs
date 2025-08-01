using DCL.UI.SceneDebugConsole.LogHistory;
using UnityEngine.UIElements;

namespace DCL.UI.SceneDebugConsole.UI.Elements
{
    [UxmlElement]
    public partial class ConsoleEntryElement : VisualElement
    {
        private const string USS_BLOCK = "console-entry";
        private const string USS_TYPE_LOG = USS_BLOCK + "--type-log";
        private const string USS_TYPE_WARNING = USS_BLOCK + "--type-warning";
        private const string USS_TYPE_ERROR = USS_BLOCK + "--type-error";
        private const string USS_MESSAGE = USS_BLOCK + "__message";
        private const string USS_COPY_ICON = USS_BLOCK + "__copy-icon";

        private readonly Label message;

        public ConsoleEntryElement()
        {
            AddToClassList(USS_BLOCK);

            var copyIcon = new VisualElement { name = "copy-icon" };
            Add(copyIcon);
            copyIcon.AddToClassList(USS_COPY_ICON);

            Add(message = new Label("Log message goes here\nAnd also here") { name = "message" });
            message.AddToClassList(USS_MESSAGE);
        }

        public void SetData(LogMessageType logMessageType, string msg)
        {
            EnableInClassList(USS_TYPE_LOG, logMessageType == LogMessageType.Log);
            EnableInClassList(USS_TYPE_WARNING, logMessageType == LogMessageType.Warning);
            EnableInClassList(USS_TYPE_ERROR, logMessageType == LogMessageType.Error);

            this.message.text = msg;
        }
    }
}
