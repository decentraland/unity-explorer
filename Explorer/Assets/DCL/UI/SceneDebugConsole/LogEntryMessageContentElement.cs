using DCL.UI.HyperlinkHandler;
using DCL.UI.SceneDebugConsole.LogHistory;
using TMPro;
using UnityEngine;

namespace DCL.UI.SceneDebugConsole
{
    public class LogEntryMessageContentElement : MonoBehaviour
    {
        [field: SerializeField] internal RectTransform messageContentRectTransform { get; private set; }
        [field: SerializeField] internal TMP_Text messageContentText { get; private set; }
        [field: SerializeField] internal TextHyperlinkHandlerElement textHyperlinkHandler { get; private set; }

        public void SetMessageContent(SceneDebugConsoleLogMessage data)
        {
            messageContentText.SetText(data.Message);
            messageContentText.color = data.Color;
            messageContentText.ForceMeshUpdate(true, true);
        }
    }
}
