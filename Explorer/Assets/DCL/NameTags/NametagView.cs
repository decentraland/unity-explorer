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
        private const float DEFAULT_MARGIN_OFFSET_HEIGHT = 0.15f;
        private const float DEFAULT_MARGIN_OFFSET_WIDTH = 0.2f;
        private const int DEFAULT_OPACITY_MAX_DISTANCE = 20;
        private const int DEFAULT_ADDITIONAL_MS_PER_CHARACTER = 20;
        private const float DEFAULT_BUBBLE_MARGIN_OFFSET_WIDTH = 0.4f;
        private const float DEFAULT_BUBBLE_MARGIN_OFFSET_HEIGHT = 0.6f;
        private const float DEFAULT_BUBBLE_ANIMATION_IN_DURATION = 0.5f;
        private const float DEFAULT_BUBBLE_ANIMATION_OUT_DURATION = 0.35f;
        private const int DEFAULT_BUBBLE_IDLE_TIME_MS = 5000;
        private const float DEFAULT_SINGLE_EMOJI_EXTRA_HEIGHT = 0.1f;
        private const float DEFAULT_SINGLE_EMOJI_SIZE = 3.5f;

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

        private readonly Color finishColor = new (1, 1, 1, 0);
        private Vector2 messageContentAnchoredPosition;
        private bool isAnimatingIn;
        private bool isWaiting;
        private Vector2 usernameFinalPosition;
        private Vector2 verifiedIconFinalPosition;
        private Vector2 verifiedIconInitialPosition;
        private Vector2 preferredSize;
        private Vector2 backgroundFinalSize;
        private Vector2 textContentInitialPosition;
        private Vector2 usernamePos;
        private float alpha;
        private float previousDistance;
        private float additionalHeight;
        private readonly Color startingTextColor = new (1, 1, 1, 0);
        private Color textColor = new (1, 1, 1, 1);
        private Color usernameTextColor = new (1, 1, 1, 1);
        private Color backgroundColor = new (1, 1, 1, 1);
        private ChatBubbleConfigurationSO? chatBubbleConfiguration;
        private bool isClaimedName;
        private CancellationTokenSource? cts;

        public void InjectConfiguration(ChatBubbleConfigurationSO chatBubbleConfigurationSo)
        {
            chatBubbleConfiguration = chatBubbleConfigurationSo;
            messageContentAnchoredPosition = new (0, chatBubbleConfiguration.bubbleMarginOffsetHeight / 3);
        }

        public void SetUsername(string username, string walletId, bool hasClaimedName)
        {
            ResetElement();

            cts.SafeCancelAndDispose();
            cts = new CancellationTokenSource();

            isClaimedName = hasClaimedName;
            VerifiedIcon.gameObject.SetActive(hasClaimedName);
            Username.text = hasClaimedName ? username : $"{username}<color=#FFFFFF66><font=\"LiberationSans SDF\">#{walletId}</font></color>";
            Username.rectTransform.sizeDelta = new Vector2(Username.preferredWidth, DEFAULT_HEIGHT);
            MessageContent.color = startingTextColor;

            float nametagMarginOffsetHeight = chatBubbleConfiguration?.nametagMarginOffsetHeight ?? DEFAULT_MARGIN_OFFSET_HEIGHT;
            float nametagMarginOffsetWidth = chatBubbleConfiguration?.nametagMarginOffsetWidth ?? DEFAULT_MARGIN_OFFSET_WIDTH;

            if (hasClaimedName)
            {
                verifiedIconInitialPosition = new Vector2(Username.rectTransform.anchoredPosition.x + (Username.preferredWidth / 2) + (VerifiedIcon.sizeDelta.x / 2) - (nametagMarginOffsetHeight / 2), 0);
                VerifiedIcon.anchoredPosition = verifiedIconInitialPosition;
                usernamePos.x = Username.rectTransform.anchoredPosition.x;
                usernamePos.x -= VerifiedIcon.sizeDelta.x / 2;
                Username.rectTransform.anchoredPosition = usernamePos;
                Background.size = new Vector2(Username.preferredWidth + nametagMarginOffsetWidth + VerifiedIcon.sizeDelta.x, Username.preferredHeight + nametagMarginOffsetHeight);
            }
            else
            {
                Username.rectTransform.anchoredPosition = Vector2.zero;
                Background.size = new Vector2(Username.preferredWidth + nametagMarginOffsetWidth, Username.preferredHeight + nametagMarginOffsetHeight);
            }
        }

        public void SetTransparency(float distance, float maxDistance)
        {
            if (Math.Abs(distance - previousDistance) < DISTANCE_THRESHOLD)
                return;

            float fullOpacityMaxDistance = chatBubbleConfiguration?.fullOpacityMaxDistance ?? DEFAULT_OPACITY_MAX_DISTANCE;

            previousDistance = distance;
            usernameTextColor = Username.color;
            alpha = alphaOverDistanceCurve.Evaluate((distance - fullOpacityMaxDistance) / (maxDistance - fullOpacityMaxDistance));
            textColor.a = distance > fullOpacityMaxDistance ? alpha : 1;
            usernameTextColor.a = distance > fullOpacityMaxDistance ? alpha : 1;
            backgroundColor.a = distance > fullOpacityMaxDistance ? alpha : 1;
            BubblePeak.color = backgroundColor;
            Background.color = backgroundColor;
            Username.color = usernameTextColor;
            VerifiedIconRenderer.color = backgroundColor;
        }

        public void SetChatMessage(string chatMessage)
        {
            cts.SafeCancelAndDispose();
            cts = new CancellationTokenSource();

            StartChatBubbleFlowAsync(chatMessage, cts.Token).Forget();
        }

        private void ResetElement()
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
        }

        private async UniTaskVoid StartChatBubbleFlowAsync(string chatMessage, CancellationToken ct)
        {
            try
            {
                if (isAnimatingIn || isWaiting)
                    await AnimateOutAsync(ct);

                await AnimateInAsync(chatMessage, ct);

                isAnimatingIn = false;
                isWaiting = true;

                await UniTask.Delay((chatBubbleConfiguration?.bubbleIdleTime ?? DEFAULT_BUBBLE_IDLE_TIME_MS) + AdditionalMessageVisibilityTimeMs(chatMessage), cancellationToken: ct);

                isWaiting = false;

                await AnimateOutAsync(ct);
            }
            catch (OperationCanceledException)
            {
                isAnimatingIn = false;
                isWaiting = false;
            }
        }

        private int AdditionalMessageVisibilityTimeMs(string chatMessage) =>
            chatMessage.Length * (chatBubbleConfiguration?.additionalMsPerCharacter ?? DEFAULT_ADDITIONAL_MS_PER_CHARACTER);

        //TODO: jobify this to improve the performance
        private async UniTask AnimateInAsync(string messageContent, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();

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
            preferredSize.x = CalculatePreferredWidth(messageContent);
            preferredSize.y += additionalHeight;
            MessageContentRectTransform.sizeDelta = preferredSize;

            //Calculate the initial message content position to animate after
            textContentInitialPosition.x = preferredSize.x / 2;
            textContentInitialPosition.y = -preferredSize.y;
            MessageContentRectTransform.anchoredPosition = textContentInitialPosition;

            float bubbleMarginOffsetWidth = chatBubbleConfiguration?.bubbleMarginOffsetWidth ?? DEFAULT_BUBBLE_MARGIN_OFFSET_WIDTH;
            float bubbleMarginOffsetHeight = chatBubbleConfiguration?.bubbleMarginOffsetHeight ?? DEFAULT_BUBBLE_MARGIN_OFFSET_HEIGHT;
            float animationInDuration = chatBubbleConfiguration?.animationInDuration ?? DEFAULT_BUBBLE_ANIMATION_IN_DURATION;

            preferredSize.x = MessageContentRectTransform.sizeDelta.x + bubbleMarginOffsetWidth;
            preferredSize.y += bubbleMarginOffsetHeight;

            //set the username final position based on previous calculations
            usernameFinalPosition.x = (-preferredSize.x / 2) + (Username.preferredWidth / 2) + (bubbleMarginOffsetWidth / 2);
            usernameFinalPosition.y = MessageContentRectTransform.sizeDelta.y + (bubbleMarginOffsetHeight / 3);

            if (isClaimedName)
            {
                verifiedIconFinalPosition.x = usernameFinalPosition.x + (Username.preferredWidth / 2) + (VerifiedIcon.sizeDelta.x / 2);
                verifiedIconFinalPosition.y = usernameFinalPosition.y;
                VerifiedIcon.DOAnchorPos(verifiedIconFinalPosition, animationInDuration).SetEase(backgroundEaseAnimationCurve).ToUniTask(cancellationToken: ct);
            }

            //Start all animations
            await UniTask.WhenAll(
                DOTween.Sequence().AppendInterval(animationInDuration / 3).Append(MessageContent.DOColor(textColor, animationInDuration / 4)).Play().ToUniTask(cancellationToken: ct),
                Username.rectTransform.DOAnchorPos(usernameFinalPosition, animationInDuration).SetEase(backgroundEaseAnimationCurve).ToUniTask(cancellationToken: ct),
                MessageContent.rectTransform.DOAnchorPos(messageContentAnchoredPosition, animationInDuration).SetEase(backgroundEaseAnimationCurve).ToUniTask(cancellationToken: ct),
                DOTween.To(() => Background.size, x => Background.size = x, preferredSize, animationInDuration).SetEase(backgroundEaseAnimationCurve).ToUniTask(cancellationToken: ct)
            );
        }

        private void SetHeightAndTextStyle(string messageContent)
        {
            if (messageContent.Contains("\\U") && messageContent.Length == EMOJI_LENGTH)
            {
                additionalHeight = chatBubbleConfiguration?.singleEmojiExtraHeight ?? DEFAULT_SINGLE_EMOJI_EXTRA_HEIGHT;
                MessageContent.fontSize = chatBubbleConfiguration?.singleEmojiSize ?? DEFAULT_SINGLE_EMOJI_SIZE;
                MessageContent.alignment = TextAlignmentOptions.Center;
            }
            else
            {
                additionalHeight = 0;
                MessageContent.fontSize = MESSAGE_CONTENT_FONT_SIZE;
                MessageContent.alignment = TextAlignmentOptions.Left;
            }
        }

        private async UniTask AnimateOutAsync(CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();

            BubblePeak.gameObject.SetActive(false);
            backgroundFinalSize.y = Username.preferredHeight + (chatBubbleConfiguration?.nametagMarginOffsetHeight ?? DEFAULT_MARGIN_OFFSET_HEIGHT);

            float nametagMarginOffsetWidth = chatBubbleConfiguration?.nametagMarginOffsetWidth ?? DEFAULT_MARGIN_OFFSET_WIDTH;
            float animationOutDuration = chatBubbleConfiguration?.animationOutDuration ?? DEFAULT_BUBBLE_ANIMATION_OUT_DURATION;

            if (isClaimedName)
            {
                backgroundFinalSize.x = Username.preferredWidth + nametagMarginOffsetWidth + VerifiedIcon.sizeDelta.x;
                Username.rectTransform.DOAnchorPos(new Vector2(-VerifiedIcon.sizeDelta.x / 2, 0), animationOutDuration / 2).SetEase(Ease.Linear).ToUniTask(cancellationToken: ct);
                VerifiedIcon.DOAnchorPos(verifiedIconInitialPosition, animationOutDuration / 2).SetEase(Ease.Linear).ToUniTask(cancellationToken: ct);
            }
            else
            {
                backgroundFinalSize.x = Username.preferredWidth + nametagMarginOffsetWidth;
                Username.rectTransform.DOAnchorPos(Vector2.zero, animationOutDuration / 2).SetEase(Ease.Linear).ToUniTask(cancellationToken: ct);
            }

            await UniTask.WhenAll(
                MessageContent.rectTransform.DOAnchorPos(textContentInitialPosition, animationOutDuration / 2).SetEase(Ease.Linear).ToUniTask(cancellationToken: ct),
                MessageContent.DOColor(finishColor, animationOutDuration / 10).ToUniTask(cancellationToken: ct),
                DOTween.To(() => Background.size, x => Background.size = x, backgroundFinalSize, animationOutDuration / 2).SetEase(Ease.Linear).ToUniTask(cancellationToken: ct)
            );
        }

        private float CalculatePreferredWidth(string messageContent)
        {
            float nametagMarginOffsetWidth = chatBubbleConfiguration?.nametagMarginOffsetWidth ?? DEFAULT_MARGIN_OFFSET_WIDTH;

            if (Username.preferredWidth + nametagMarginOffsetWidth + (isClaimedName ? VerifiedIcon.sizeDelta.x : 0) > MessageContent.preferredWidth)
                return Username.preferredWidth + nametagMarginOffsetWidth + (isClaimedName ? VerifiedIcon.sizeDelta.x : 0);

            if (MessageContent.GetPreferredValues(messageContent, MaxWidth, 0).x < MaxWidth)
                return MessageContent.GetPreferredValues(messageContent, MaxWidth, 0).x;

            return MaxWidth;
        }
    }
}
