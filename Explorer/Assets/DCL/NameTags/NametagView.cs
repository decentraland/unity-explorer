using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using Utility;
using Random = UnityEngine.Random;
using Vector2 = UnityEngine.Vector2;
using DG.Tweening;
using System;

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
        public SpriteRenderer BubblePeak { get; private set; }

        [field: SerializeField]
        public RectTransform MessageContentRectTransform { get; private set; }

        [field: SerializeField]
        internal AnimationCurve backgroundEaseAnimationCurve { get; private set; }

        [field: SerializeField]
        internal AnimationCurve alphaOverDistanceCurve { get; private set; }

        public string Id;

        [field: SerializeField]
        public float MaxWidth { get; private set; }

        private readonly Color finishColor = new (1,1,1,0);
        private Vector2 messageContentAnchoredPosition;

        private bool isBubbleExpanded;
        private Vector2 usernameFinalPosition;
        private Vector2 preferredSize;
        private Vector2 backgroundFinalSize;
        private Vector2 textContentInitialPosition;

        private float alpha;
        private float previousDistance;
        private const float DISTANCE_THRESHOLD = 0.1f;

        private readonly Color startingTextColor = new (1,1,1,0);
        private Color textColor = new (1,1,1,1);
        private Color usernameTextColor = new (1,1,1,1);
        private Color backgroundColor = new (1, 1, 1, 1);
        private ChatBubbleConfigurationSO chatBubbleConfiguration;

        public void InjectConfiguration(ChatBubbleConfigurationSO chatBubbleConfigurationSo)
        {
            chatBubbleConfiguration = chatBubbleConfigurationSo;
            messageContentAnchoredPosition = new (0,chatBubbleConfiguration.bubbleMarginOffsetHeight / 3);
        }

        public void SetUsername(string username)
        {
            Username.text = username;
            Username.rectTransform.sizeDelta = new Vector2(Username.preferredWidth, Username.preferredHeight + chatBubbleConfiguration.nametagMarginOffsetHeight);
            Username.rectTransform.anchoredPosition = Vector2.zero;
            Background.size = new Vector2(Username.preferredWidth + chatBubbleConfiguration.nametagMarginOffsetWidth, Username.preferredHeight + chatBubbleConfiguration.nametagMarginOffsetHeight);
            MessageContent.color = startingTextColor;
        }

        public void SetTransparency(float distance, float maxDistance)
        {
            if(Math.Abs(distance - previousDistance) < DISTANCE_THRESHOLD)
                return;

            previousDistance = distance;
            usernameTextColor = Username.color;
            alpha = alphaOverDistanceCurve.Evaluate((distance - chatBubbleConfiguration.fullOpacityMaxDistance) / (maxDistance - chatBubbleConfiguration.fullOpacityMaxDistance) );
            textColor.a = distance > chatBubbleConfiguration.fullOpacityMaxDistance ? alpha : 1;
            usernameTextColor.a = distance > chatBubbleConfiguration.fullOpacityMaxDistance ? alpha : 1;
            backgroundColor.a = distance > chatBubbleConfiguration.fullOpacityMaxDistance ? alpha : 1;
            BubblePeak.color = backgroundColor;
            Background.color = backgroundColor;
            Username.color = usernameTextColor;
        }

        public void SetChatMessage(string chatMessage) =>
            StartChatBubbleFlowAsync(chatMessage).Forget();

        private async UniTaskVoid StartChatBubbleFlowAsync(string chatMessage)
        {
            if(!isBubbleExpanded)
                AnimateIn(chatMessage);

            await UniTask.Delay(chatBubbleConfiguration.bubbleIdleTime + AdditionalMessageVisibilityTimeMs(chatMessage));
            AnimateOut();
        }

        private int AdditionalMessageVisibilityTimeMs(string chatMessage) =>
            chatMessage.Length * chatBubbleConfiguration.additionalMsPerCharacter;

        //TODO: jobify this to improve the performance
        private void AnimateIn(string messageContent)
        {
            MessageContent.gameObject.SetActive(true);
            BubblePeak.gameObject.SetActive(true);
            isBubbleExpanded = true;

            //Calculate message content preferred size with fixed width
            preferredSize = MessageContent.GetPreferredValues(messageContent, MaxWidth, 0);
            preferredSize.x =  CalculatePreferredWidth(messageContent);
            MessageContentRectTransform.sizeDelta = preferredSize;

            //Calculate the initial message content position to animate after
            textContentInitialPosition.x = preferredSize.x / 2;
            textContentInitialPosition.y = -preferredSize.y;
            MessageContentRectTransform.anchoredPosition = textContentInitialPosition;

            //Set message content and calculate the preferred size of the background with the addition of a margin
            MessageContent.text = messageContent;
            preferredSize.x = MessageContentRectTransform.sizeDelta.x + chatBubbleConfiguration.bubbleMarginOffsetWidth;
            preferredSize.y += chatBubbleConfiguration.bubbleMarginOffsetHeight;

            //set the username final position based on previous calculations
            usernameFinalPosition.x = (-preferredSize.x / 2) + (Username.preferredWidth / 2) + (chatBubbleConfiguration.bubbleMarginOffsetWidth / 2);
            usernameFinalPosition.y = MessageContentRectTransform.sizeDelta.y + (chatBubbleConfiguration.bubbleMarginOffsetHeight / 3);

            //Start all animations
            DOTween.Sequence().AppendInterval(chatBubbleConfiguration.animationDuration / 3).Append(MessageContent.DOColor(textColor, chatBubbleConfiguration.animationDuration / 4)).Play();
            Username.rectTransform.DOAnchorPos(usernameFinalPosition, chatBubbleConfiguration.animationDuration).SetEase(backgroundEaseAnimationCurve);
            MessageContent.rectTransform.DOAnchorPos(messageContentAnchoredPosition, chatBubbleConfiguration.animationDuration).SetEase(backgroundEaseAnimationCurve);
            DOTween.To(() => Background.size, x=> Background.size = x, preferredSize, chatBubbleConfiguration.animationDuration).SetEase(backgroundEaseAnimationCurve);
        }

        private float CalculatePreferredWidth(string messageContent)
        {
            if (Username.GetParsedText().Length > messageContent.Length)
                return Username.preferredWidth + chatBubbleConfiguration.nametagMarginOffsetWidth;

            if(MessageContent.GetPreferredValues(messageContent, MaxWidth, 0).x < MaxWidth)
                return MessageContent.GetPreferredValues(messageContent, MaxWidth, 0).x;

            return MaxWidth;
        }

        private void AnimateOut()
        {
            isBubbleExpanded = false;
            BubblePeak.gameObject.SetActive(false);

            backgroundFinalSize.x = Username.preferredWidth + chatBubbleConfiguration.nametagMarginOffsetWidth;
            backgroundFinalSize.y = Username.preferredHeight + chatBubbleConfiguration.nametagMarginOffsetHeight;

            Username.rectTransform.DOAnchorPos(Vector2.zero, chatBubbleConfiguration.animationDuration / 2).SetEase(Ease.Linear);
            MessageContent.rectTransform.DOAnchorPos(textContentInitialPosition, chatBubbleConfiguration.animationDuration / 2).SetEase(Ease.Linear);
            MessageContent.DOColor(finishColor, chatBubbleConfiguration.animationDuration / 10);
            DOTween.To(() => Background.size, x=> Background.size = x, backgroundFinalSize, chatBubbleConfiguration.animationDuration / 2).SetEase(Ease.Linear);
        }

    }
}
