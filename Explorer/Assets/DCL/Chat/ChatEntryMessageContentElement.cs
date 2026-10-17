using DCL.UI.HyperlinkHandler;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.Chat
{
    public class ChatEntryMessageContentElement : MonoBehaviour
    {
        [field: SerializeField] internal RectTransform messageContentRectTransform { get; private set; } = null!;
        [field: SerializeField] internal ContentSizeFitter messageContentSizeFitter { get; private set; } = null!;
        [field: SerializeField] internal TMP_Text messageContentText { get; private set; } = null!;

        [field: SerializeField] private CanvasGroup messageContentCanvas = null!;

        private string lastSetText = string.Empty;

        public void SetMessageContent(string content)
        {
            if (lastSetText == content)
                return;

            lastSetText = content;
            messageContentText.SetText(content);

            //Force mesh is needed otherwise entryText.GetParsedText() in CalculatePreferredWidth will return the original text
            //of the previous frame, also data for links would not be updated either.
            messageContentText.ForceMeshUpdate(true, true);
            messageContentSizeFitter.SetLayoutVertical();
        }

        public void GreyOut(float opacity)
        {
            messageContentCanvas.alpha = 1.0f - opacity;
        }
    }
}
