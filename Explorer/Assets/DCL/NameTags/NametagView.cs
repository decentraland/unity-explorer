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
        public float FixedWidth { get; private set; }

        private const float NAMETAG_MARGIN_OFFSET_WIDTH = 0.2f;
        private const float NAMETAG_MARGIN_OFFSET_HEIGHT = 0.15f;
        private const float BUBBLE_MARGIN_OFFSET_WIDTH = 0.4f;
        private const float BUBBLE_MARGIN_OFFSET_HEIGHT = 0.6f;
        private const float ANIMATION_DURATION = 0.7f;
        private const float FULL_OPACITY_MAX_DISTANCE = 8.5f;
        private const int CHAT_BUBBLE_IDLE_TIME = 6000;
        private readonly Color finishColor = new (1,1,1,0);
        private static readonly Vector2 MESSAGE_CONTENT_ANCHORED_POSITION = new (0,BUBBLE_MARGIN_OFFSET_HEIGHT / 3);

        private bool isBubbleExpanded;
        private Vector2 usernameFinalPosition;
        private Vector2 preferredSize;
        private Vector2 backgroundFinalSize;
        private Vector2 textContentInitialPosition;

        private float alpha;
        private float previousDistance;
        private const float DISTANCE_THRESHOLD = 0.1f;

        private Color textColor = new (1,1,1,1);
        private Color usernameTextColor = new (1,1,1,1);
        private Color backgroundColor = new (1, 1, 1, 1);

        public void SetUsername(string username)
        {
            Username.text = username;
            Username.rectTransform.sizeDelta = new Vector2(Username.preferredWidth, Username.preferredHeight + NAMETAG_MARGIN_OFFSET_HEIGHT);
            Username.rectTransform.anchoredPosition = Vector2.zero;
            Background.size = new Vector2(Username.preferredWidth + NAMETAG_MARGIN_OFFSET_WIDTH, Username.preferredHeight + NAMETAG_MARGIN_OFFSET_HEIGHT);
        }
        
        public void SetTransparency(float distance, float maxDistance)
        {
            if(Math.Abs(distance - previousDistance) < DISTANCE_THRESHOLD)
                return;

            previousDistance = distance;
            usernameTextColor = Username.color;
            alpha = alphaOverDistanceCurve.Evaluate((distance - FULL_OPACITY_MAX_DISTANCE) / (maxDistance - FULL_OPACITY_MAX_DISTANCE) );
            textColor.a = distance > FULL_OPACITY_MAX_DISTANCE ? alpha : 1;
            usernameTextColor.a = distance > FULL_OPACITY_MAX_DISTANCE ? alpha : 1;
            backgroundColor.a = distance > FULL_OPACITY_MAX_DISTANCE ? alpha : 1;
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

            await UniTask.Delay(CHAT_BUBBLE_IDLE_TIME);
            AnimateOut();
        }

        //TODO: jobify this to improve the performance
        private void AnimateIn(string messageContent)
        {
            MessageContent.gameObject.SetActive(true);
            BubblePeak.gameObject.SetActive(true);
            isBubbleExpanded = true;

            //Calculate message content preferred size with fixed width
            preferredSize = MessageContent.GetPreferredValues(messageContent, FixedWidth, 0);
            preferredSize.x = MessageContentRectTransform.sizeDelta.x;
            MessageContentRectTransform.sizeDelta = preferredSize;

            //Calculate the initial message content position to animate after
            textContentInitialPosition.x = preferredSize.x / 2;
            textContentInitialPosition.y = -preferredSize.y;
            MessageContentRectTransform.anchoredPosition = textContentInitialPosition;

            //Set message content and calculate the preferred size of the background with the addition of a margin
            MessageContent.text = messageContent;
            preferredSize.x = MessageContentRectTransform.sizeDelta.x + BUBBLE_MARGIN_OFFSET_WIDTH;
            preferredSize.y += BUBBLE_MARGIN_OFFSET_HEIGHT;

            //set the username final position based on previous calculations
            usernameFinalPosition.x = (-preferredSize.x / 2) + (Username.preferredWidth / 2) + (BUBBLE_MARGIN_OFFSET_WIDTH / 2);
            usernameFinalPosition.y = MessageContentRectTransform.sizeDelta.y + (BUBBLE_MARGIN_OFFSET_HEIGHT / 3);

            //Start all animations
            MessageContent.DOColor(textColor, ANIMATION_DURATION);
            Username.rectTransform.DOAnchorPos(usernameFinalPosition, ANIMATION_DURATION).SetEase(backgroundEaseAnimationCurve);
            MessageContent.rectTransform.DOAnchorPos(MESSAGE_CONTENT_ANCHORED_POSITION, ANIMATION_DURATION).SetEase(backgroundEaseAnimationCurve);
            DOTween.To(() => Background.size, x=> Background.size = x, preferredSize, ANIMATION_DURATION).SetEase(backgroundEaseAnimationCurve);
        }

        private void AnimateOut()
        {
            isBubbleExpanded = false;
            BubblePeak.gameObject.SetActive(false);

            backgroundFinalSize.x = Username.preferredWidth + NAMETAG_MARGIN_OFFSET_WIDTH;
            backgroundFinalSize.y = Username.preferredHeight + NAMETAG_MARGIN_OFFSET_HEIGHT;

            Username.rectTransform.DOAnchorPos(Vector2.zero, ANIMATION_DURATION / 2).SetEase(Ease.Linear);
            MessageContent.rectTransform.DOAnchorPos(textContentInitialPosition, ANIMATION_DURATION / 2).SetEase(Ease.Linear);
            MessageContent.DOColor(finishColor, ANIMATION_DURATION / 4);
            DOTween.To(() => Background.size, x=> Background.size = x, backgroundFinalSize, ANIMATION_DURATION / 2).SetEase(Ease.Linear);
        }

    }
}
