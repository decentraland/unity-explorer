using Cysharp.Threading.Tasks;
using System;
using TMPro;
using UnityEngine;
using Utility;
using Random = UnityEngine.Random;
using Vector2 = UnityEngine.Vector2;

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

        [field: SerializeField]
        public float FixedWidth { get; private set; }
        private const float MARGIN_OFFSET = 0.5f;
        private static Vector2 messageContentAnchoredPosition = new (0,MARGIN_OFFSET / 3);

        private bool isBubbleExpanded = false;

        private void Start()
        {
            GenerateRandomMsgsAsync().Forget();
        }

        public void SetUsername(string username)
        {
            Username.text = username;
            Username.rectTransform.sizeDelta = new Vector2(Username.preferredWidth, Username.preferredHeight + 0.15f);
            Background.size = new Vector2(Username.preferredWidth + 0.2f, Username.preferredHeight + 0.15f);

            //Animate("really long string really long string really long string really long string really long string really long string really long string ");
        }

        private async UniTaskVoid GenerateRandomMsgsAsync()
        {
            do
            {
                StartChatBubbleFlowAsync(StringUtils.GenerateRandomString(Random.Range(0,250))).Forget();
                await UniTask.Delay(Random.Range(5000,10000));
            }
            while (true);
        }

        public void SetChatMessage(string chatMessage)
        {
            StartChatBubbleFlowAsync(chatMessage).Forget();
        }

        private async UniTaskVoid StartChatBubbleFlowAsync(string chatMessage)
        {
            if(!isBubbleExpanded)
                AnimateIn(chatMessage);

            await UniTask.Delay(2000);
            AnimateOut();
        }

        private void AnimateIn(string messageContent)
        {
            MessageContent.gameObject.SetActive(true);
            isBubbleExpanded = true;
            
            Vector2 preferredSize = MessageContent.GetPreferredValues(messageContent, FixedWidth, 0);
            preferredSize.x = MessageContentRectTransform.sizeDelta.x;
            MessageContentRectTransform.sizeDelta = preferredSize;
            MessageContentRectTransform.anchoredPosition = messageContentAnchoredPosition;
            MessageContent.text = messageContent;
            preferredSize.x = MessageContentRectTransform.sizeDelta.x + MARGIN_OFFSET;
            preferredSize.y += MARGIN_OFFSET;
            Username.rectTransform.anchoredPosition = new Vector2((-preferredSize.x / 2) + (Username.preferredWidth / 2) + (MARGIN_OFFSET / 2), MessageContentRectTransform.sizeDelta.y + (MARGIN_OFFSET / 3));
            Background.size = preferredSize;
        }

        private void AnimateOut()
        {
            MessageContent.gameObject.SetActive(false);
            isBubbleExpanded = false;
            Username.rectTransform.anchoredPosition = Vector2.zero;
            Background.size = new Vector2(Username.preferredWidth + 0.2f, Username.preferredHeight + 0.15f);
        }

    }
}
