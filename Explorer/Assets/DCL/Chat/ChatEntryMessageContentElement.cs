using DCL.UI.HyperlinkHandler;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.Chat
{
    public class ChatEntryMessageContentElement : MonoBehaviour
    {
        [field: SerializeField] internal RectTransform messageContentRectTransform { get; private set; }
        [field: SerializeField] internal ContentSizeFitter messageContentSizeFitter { get; private set; }
        [field: SerializeField] internal TMP_Text messageContentText { get; private set; }
        [field: SerializeField] internal HyperlinkHandlerElement hyperlinkHandler { get; private set; }

        public void SetMessageContent(string content)
        {
            messageContentText.SetText(content);

            //Force mesh is needed otherwise entryText.GetParsedText() in CalculatePreferredWidth will return the original text
            //of the previous frame, also data for links and whatnot would not be updated either even after many seconds.
            messageContentText.ForceMeshUpdate(true, true);
            messageContentSizeFitter.SetLayoutVertical();
        }
    }
}
