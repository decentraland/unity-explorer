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
        [field: SerializeField] internal TextHyperlinkHandlerElement textHyperlinkHandler { get; private set; }

        [field: SerializeField] private CanvasGroup messageContentCanvas;

        public void SetMessageContent(string content)
        {
            messageContentText.SetText(content);

            //Force mesh is needed otherwise entryText.GetParsedText() in CalculatePreferredWidth will return the original text
            //of the previous frame, also data for links would not be updated either.
            messageContentText.ForceMeshUpdate(true, true);
            messageContentSizeFitter.SetLayoutVertical();
        }

        public void GreyOut(bool greyOut, float opacity)
        {
            messageContentCanvas.alpha = greyOut ? 1.0f - opacity : 1.0f;
        }
    }
}
