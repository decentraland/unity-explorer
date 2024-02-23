using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using Utility;
using Random = UnityEngine.Random;
using Vector2 = UnityEngine.Vector2;
using DG.Tweening;

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
        public float FixedWidth { get; private set; }
        private const float MARGIN_OFFSET_WIDTH = 0.5f;
        private const float MARGIN_OFFSET_HEIGHT = 0.7f;
        private const float ANIMATION_DURATION = 0.7f;
        private readonly Color startColor = new (1,1,1,1);
        private readonly Color finishColor = new (1,1,1,0);
        private static readonly Vector2 MESSAGE_CONTENT_ANCHORED_POSITION = new (0,MARGIN_OFFSET_HEIGHT / 3);

        private bool isBubbleExpanded;
        private Vector2 usernameFinalPosition;
        private Vector2 preferredSize;
        private Vector2 backgroundFinalSize;
        private Vector2 textContentInitialPosition;

        private void Start()
        {
            SetUsername(StringUtils.GenerateRandomString(Random.Range(3,10)));
            GenerateRandomMsgsAsync().Forget();
        }

        public void SetUsername(string username)
        {
            Username.text = username;
            Username.rectTransform.sizeDelta = new Vector2(Username.preferredWidth, Username.preferredHeight + 0.2f);
            Username.rectTransform.anchoredPosition = Vector2.zero;
            Background.size = new Vector2(Username.preferredWidth + 0.3f, Username.preferredHeight + 0.2f);
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

            await UniTask.Delay(4000);
            AnimateOut();
        }

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
            preferredSize.x = MessageContentRectTransform.sizeDelta.x + MARGIN_OFFSET_WIDTH;
            preferredSize.y += MARGIN_OFFSET_HEIGHT;

            //set the username final position based on previous calculations
            usernameFinalPosition.x = (-preferredSize.x / 2) + (Username.preferredWidth / 2) + (MARGIN_OFFSET_WIDTH / 2);
            usernameFinalPosition.y = MessageContentRectTransform.sizeDelta.y + (MARGIN_OFFSET_HEIGHT / 3);

            //Start all animations
            MessageContent.DOColor(startColor, ANIMATION_DURATION);
            Username.rectTransform.DOAnchorPos(usernameFinalPosition, ANIMATION_DURATION).SetEase(backgroundEaseAnimationCurve);
            MessageContent.rectTransform.DOAnchorPos(MESSAGE_CONTENT_ANCHORED_POSITION, ANIMATION_DURATION).SetEase(backgroundEaseAnimationCurve);
            DOTween.To(() => Background.size, x=> Background.size = x, preferredSize, ANIMATION_DURATION).SetEase(backgroundEaseAnimationCurve);
        }

        private void AnimateOut()
        {
            isBubbleExpanded = false;
            BubblePeak.gameObject.SetActive(false);

            backgroundFinalSize.x = Username.preferredWidth + 0.3f;
            backgroundFinalSize.y = Username.preferredHeight + 0.2f;

            Username.rectTransform.DOAnchorPos(Vector2.zero, ANIMATION_DURATION / 2).SetEase(Ease.Linear);
            MessageContent.rectTransform.DOAnchorPos(textContentInitialPosition, ANIMATION_DURATION / 2).SetEase(Ease.Linear);
            MessageContent.DOColor(finishColor, ANIMATION_DURATION / 4);
            DOTween.To(() => Background.size, x=> Background.size = x, backgroundFinalSize, ANIMATION_DURATION / 2).SetEase(Ease.Linear);
        }

    }
}
