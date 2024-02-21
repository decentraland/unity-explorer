using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;

namespace DCL.Nametags
{
    public class NametagView : MonoBehaviour
    {
        [field: SerializeField]
        public TMP_Text Username { get; private set; }

        [field: SerializeField]
        public TMP_Text MessageContent { get; private set; }

        [field: SerializeField]
        public SpriteRenderer Background { get; private set; }

        [field: SerializeField]
        public RectTransform MessageContentRectTransform { get; private set; }

        private bool isBubbleExpanded = false;

        public void SetUsername(string username)
        {
            Username.text = username;
            Username.rectTransform.sizeDelta = new Vector2(Username.preferredWidth, Username.preferredHeight + 0.15f);
            Background.size = new Vector2(Username.preferredWidth + 0.2f, Username.preferredHeight + 0.15f);

            //Animate("really long string really long string really long string really long string really long string really long string really long string ");
        }

        public void SetChatMessage(string chatMessage)
        {
            StartChatBubbleFlowAsync(chatMessage).Forget();
        }

        private async UniTaskVoid StartChatBubbleFlowAsync(string chatMessage)
        {
            if(!isBubbleExpanded)
                AnimateIn(chatMessage);

            await UniTask.Delay(5000);
            AnimateOut();
        }

        private void AnimateIn(string messageContent)
        {
            isBubbleExpanded = true;
            Vector2 preferredSize = MessageContent.GetPreferredValues(messageContent, MessageContentRectTransform.sizeDelta.x, 0);
            preferredSize.x = MessageContentRectTransform.sizeDelta.x;
            Background.size = preferredSize;
        }

        private void AnimateOut()
        {
            isBubbleExpanded = false;
        }

    }
}
