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
            messageContentText.text = content;

            //Force mesh is needed otherwise entryText.GetParsedText() in CalculatePreferredWidth will return the original text
            //of the previous frame
            messageContentText.ForceMeshUpdate();
            messageContentSizeFitter.SetLayoutVertical();
        }
    }
}
