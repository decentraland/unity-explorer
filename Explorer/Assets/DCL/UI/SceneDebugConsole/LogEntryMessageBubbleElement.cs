using DCL.UI.SceneDebugConsole.LogHistory;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DCL.UI.SceneDebugConsole
{
    /// <summary>
    ///     This class represents the part of the chat entry that contains the chat bubble, so its where we display the text of the message
    ///     and also now we display a button that when clicked opens an option panel
    /// </summary>
    public class LogEntryMessageBubbleElement : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [field: SerializeField] internal Color backgroundDefaultColor { get; private set; }
        [field: SerializeField] internal Color backgroundMentionedColor { get; private set; }
        [field: SerializeField] internal RectTransform backgroundRectTransform { get; private set; }
        [field: SerializeField] internal Image backgroundImage { get; private set; }
        [field: SerializeField] internal LogEntryMessageContentElement messageContentElement { get; private set; }
        [field: SerializeField] internal LogEntryConfigurationSO configurationSo { get; private set; }

        private Vector2 backgroundSize;

        public void OnPointerEnter(PointerEventData eventData)
        {
        }

        public void OnPointerExit(PointerEventData eventData)
        {
        }

        /// <summary>
        ///  Sets the log message data into the log bubble, adapting the background size accordingly and changing the color & outline if it's a mention
        /// </summary>
        /// <param name="data"> a SceneDebugConsoleLogMessage </param>
        public void SetMessageData(SceneDebugConsoleLogMessage data)
        {
            messageContentElement.SetMessageContent(data);
        }
    }
}
