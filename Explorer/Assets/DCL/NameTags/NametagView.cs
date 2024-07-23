using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using Vector2 = UnityEngine.Vector2;
using DG.Tweening;
using System;
using System.Threading;
using Utility;

namespace DCL.Nametags
{
    public class NametagView : MonoBehaviour
    {
        private const int EMOJI_LENGTH = 10;
        private const float DISTANCE_THRESHOLD = 0.1f;
        private const float DEFAULT_HEIGHT = 0.3f;
        private const float MESSAGE_CONTENT_FONT_SIZE = 1.3f;

        [field: SerializeField]
        public TMP_Text Username { get; private set; }

        [field: SerializeField]
        public RectTransform VerifiedIcon { get; private set; }

        [field: SerializeField]
        public SpriteRenderer VerifiedIconRenderer { get; private set; }

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

        private bool isAnimatingIn;
        private bool isWaiting;
        private bool isAnimatingOut;

        private Vector2 usernameFinalPosition;
        private Vector2 verifiedIconFinalPosition;
        private Vector2 verifiedIconInitialPosition;
        private Vector2 preferredSize;
        private Vector2 backgroundFinalSize;
        private Vector2 textContentInitialPosition;
        private Vector2 usernamePos;

        private float alpha;
        private float previousDistance;

        private readonly Color startingTextColor = new (1,1,1,0);
        private Color textColor = new (1,1,1,1);
        private Color usernameTextColor = new (1,1,1,1);
        private Color backgroundColor = new (1, 1, 1, 1);
        private ChatBubbleConfigurationSO chatBubbleConfiguration;
        private bool isClaimedName;
        private CancellationTokenSource cts;

        public void InjectConfiguration(ChatBubbleConfigurationSO chatBubbleConfigurationSo)
        {
            chatBubbleConfiguration = chatBubbleConfigurationSo;
            messageContentAnchoredPosition = new (0,chatBubbleConfiguration.bubbleMarginOffsetHeight / 3);
        }

        public void ResetElement()
        {
            preferredSize = Vector2.zero;
            backgroundFinalSize = Vector2.zero;
            textContentInitialPosition = Vector2.zero;
            usernamePos = Vector2.zero;
            Username.text = string.Empty;
            Username.rectTransform.anchoredPosition = Vector2.zero;
            MessageContent.text = string.Empty;
            Background.size = Vector2.zero;
            previousDistance = 0;
            cts.SafeCancelAndDispose();
        }

        public void SetUsername(string username, string walletId, bool hasClaimedName)
        {
            ResetElement();
            isClaimedName = hasClaimedName;
            VerifiedIcon.gameObject.SetActive(hasClaimedName);
            Username.text = hasClaimedName ? username : $"{username}<color=#FFFFFF66><font=\"LiberationSans SDF\">#{walletId}</font></color>";
            Username.rectTransform.sizeDelta = new Vector2(Username.preferredWidth, DEFAULT_HEIGHT);
            MessageContent.color = startingTextColor;

            if (hasClaimedName)
            {
                verifiedIconInitialPosition = new Vector2(Username.rectTransform.anchoredPosition.x + (Username.preferredWidth / 2) + (VerifiedIcon.sizeDelta.x / 2) - (chatBubbleConfiguration.nametagMarginOffsetHeight / 2), 0);
                VerifiedIcon.anchoredPosition = verifiedIconInitialPosition;
                usernamePos.x = Username.rectTransform.anchoredPosition.x;
                usernamePos.x -= VerifiedIcon.sizeDelta.x / 2;
                Username.rectTransform.anchoredPosition = usernamePos;
                Background.size = new Vector2(Username.preferredWidth + chatBubbleConfiguration.nametagMarginOffsetWidth + VerifiedIcon.sizeDelta.x, Username.preferredHeight + chatBubbleConfiguration.nametagMarginOffsetHeight);
            }
            else
            {
                Username.rectTransform.anchoredPosition = Vector2.zero;
                Background.size = new Vector2(Username.preferredWidth + chatBubbleConfiguration.nametagMarginOffsetWidth, Username.preferredHeight + chatBubbleConfiguration.nametagMarginOffsetHeight);
            }
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
            VerifiedIconRenderer.color = backgroundColor;
        }

        public void SetChatMessage(string chatMessage)
        {
            cts.SafeCancelAndDispose();
            cts = new CancellationTokenSource();

            StartChatBubbleFlowAsync(chatMessage).Forget();
        }

        private async UniTaskVoid StartChatBubbleFlowAsync(string chatMessage)
        {
            if (isAnimatingIn || isWaiting)
                await AnimateOutAsync();

            await AnimateInAsync(chatMessage);
            isAnimatingIn = false;
            isWaiting = true;
            await UniTask.Delay(chatBubbleConfiguration.bubbleIdleTime + AdditionalMessageVisibilityTimeMs(chatMessage), cancellationToken: cts.Token);
            isWaiting = false;
            await AnimateOutAsync();
            isAnimatingOut = false;
        }

        private int AdditionalMessageVisibilityTimeMs(string chatMessage) =>
            chatMessage.Length * chatBubbleConfiguration.additionalMsPerCharacter;

        private float additionalHeight;
        //TODO: jobify this to improve the performance
        private async UniTask AnimateInAsync(string messageContent)
        {
            SetHeightAndTextStyle(messageContent);
            isAnimatingIn = true;
            MessageContent.gameObject.SetActive(true);
            BubblePeak.gameObject.SetActive(true);

            //Set message content and calculate the preferred size of the background with the addition of a margin
            MessageContent.text = messageContent;
            //Force mesh is needed otherwise entryText.GetParsedText() in CalculatePreferredWidth will return the original text
            //of the previous frame
            MessageContent.ForceMeshUpdate();

            //Calculate message content preferred size with fixed width
            preferredSize = MessageContent.GetPreferredValues(messageContent, MaxWidth, 0);
            preferredSize.x =  CalculatePreferredWidth(messageContent);
            preferredSize.y += additionalHeight;
            MessageContentRectTransform.sizeDelta = preferredSize;

            //Calculate the initial message content position to animate after
            textContentInitialPosition.x = preferredSize.x / 2;
            textContentInitialPosition.y = -preferredSize.y;
            MessageContentRectTransform.anchoredPosition = textContentInitialPosition;

            preferredSize.x = MessageContentRectTransform.sizeDelta.x + chatBubbleConfiguration.bubbleMarginOffsetWidth;
            preferredSize.y += chatBubbleConfiguration.bubbleMarginOffsetHeight;

            //set the username final position based on previous calculations
            usernameFinalPosition.x = (-preferredSize.x / 2) + (Username.preferredWidth / 2) + (chatBubbleConfiguration.bubbleMarginOffsetWidth / 2);
            usernameFinalPosition.y = MessageContentRectTransform.sizeDelta.y + (chatBubbleConfiguration.bubbleMarginOffsetHeight / 3);

            if (isClaimedName)
            {
                verifiedIconFinalPosition.x = usernameFinalPosition.x + (Username.preferredWidth / 2) + (VerifiedIcon.sizeDelta.x / 2);
                verifiedIconFinalPosition.y = usernameFinalPosition.y;
                VerifiedIcon.DOAnchorPos(verifiedIconFinalPosition, chatBubbleConfiguration.animationInDuration).SetEase(backgroundEaseAnimationCurve).ToUniTask(cancellationToken: cts.Token);
            }

            //Start all animations
            await UniTask.WhenAll(
                DOTween.Sequence().AppendInterval(chatBubbleConfiguration.animationInDuration / 3).Append(MessageContent.DOColor(textColor, chatBubbleConfiguration.animationInDuration / 4)).Play().ToUniTask(cancellationToken: cts.Token),
                Username.rectTransform.DOAnchorPos(usernameFinalPosition, chatBubbleConfiguration.animationInDuration).SetEase(backgroundEaseAnimationCurve).ToUniTask(cancellationToken: cts.Token),
                MessageContent.rectTransform.DOAnchorPos(messageContentAnchoredPosition, chatBubbleConfiguration.animationInDuration).SetEase(backgroundEaseAnimationCurve).ToUniTask(cancellationToken: cts.Token),
                DOTween.To(() => Background.size, x=> Background.size = x, preferredSize, chatBubbleConfiguration.animationInDuration).SetEase(backgroundEaseAnimationCurve).ToUniTask(cancellationToken: cts.Token)
                );
        }

        private void SetHeightAndTextStyle(string messageContent)
        {
            if (messageContent.Contains("\\U") && messageContent.Length == EMOJI_LENGTH)
            {
                additionalHeight = chatBubbleConfiguration.singleEmojiExtraHeight;
                MessageContent.fontSize = chatBubbleConfiguration.singleEmojiSize;
                MessageContent.alignment = TextAlignmentOptions.Center;
            }
            else
            {
                additionalHeight = 0;
                MessageContent.fontSize = MESSAGE_CONTENT_FONT_SIZE;
                MessageContent.alignment = TextAlignmentOptions.Left;
            }
        }

        private async UniTask AnimateOutAsync()
        {
            isAnimatingOut = true;
            BubblePeak.gameObject.SetActive(false);
            backgroundFinalSize.y = Username.preferredHeight + chatBubbleConfiguration.nametagMarginOffsetHeight;

            if (isClaimedName)
            {
                backgroundFinalSize.x = Username.preferredWidth + chatBubbleConfiguration.nametagMarginOffsetWidth + VerifiedIcon.sizeDelta.x;
                Username.rectTransform.DOAnchorPos(new Vector2(-VerifiedIcon.sizeDelta.x / 2, 0), chatBubbleConfiguration.animationOutDuration / 2).SetEase(Ease.Linear).ToUniTask(cancellationToken: cts.Token);
                VerifiedIcon.DOAnchorPos(verifiedIconInitialPosition, chatBubbleConfiguration.animationOutDuration / 2).SetEase(Ease.Linear).ToUniTask(cancellationToken: cts.Token);
            }
            else
            {
                backgroundFinalSize.x = Username.preferredWidth + chatBubbleConfiguration.nametagMarginOffsetWidth;
                Username.rectTransform.DOAnchorPos(Vector2.zero, chatBubbleConfiguration.animationOutDuration / 2).SetEase(Ease.Linear).ToUniTask(cancellationToken: cts.Token);
            }

            await UniTask.WhenAll(
                MessageContent.rectTransform.DOAnchorPos(textContentInitialPosition, chatBubbleConfiguration.animationOutDuration / 2).SetEase(Ease.Linear).ToUniTask(cancellationToken: cts.Token),
                MessageContent.DOColor(finishColor, chatBubbleConfiguration.animationOutDuration / 10).ToUniTask(cancellationToken: cts.Token),
                DOTween.To(() => Background.size, x => Background.size = x, backgroundFinalSize, chatBubbleConfiguration.animationOutDuration / 2).SetEase(Ease.Linear).ToUniTask(cancellationToken: cts.Token)
                );
        }

        private float CalculatePreferredWidth(string messageContent)
        {
            if(Username.preferredWidth + chatBubbleConfiguration.nametagMarginOffsetWidth + (isClaimedName ? VerifiedIcon.sizeDelta.x : 0) > MessageContent.preferredWidth)
                return Username.preferredWidth + chatBubbleConfiguration.nametagMarginOffsetWidth + (isClaimedName ? VerifiedIcon.sizeDelta.x : 0);

            if(MessageContent.GetPreferredValues(messageContent, MaxWidth, 0).x < MaxWidth)
                return MessageContent.GetPreferredValues(messageContent, MaxWidth, 0).x;

            return MaxWidth;
        }

    }
}
