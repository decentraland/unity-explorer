using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using Vector2 = UnityEngine.Vector2;
using DG.Tweening;
using System;
using System.Text.RegularExpressions;
using System.Threading;
using Utility;

namespace DCL.Nametags
{
    public class NametagView : MonoBehaviour
    {
        private const float DISTANCE_THRESHOLD = 0.1f;
        private const float DEFAULT_HEIGHT = 0.3f;
        private const float MESSAGE_CONTENT_FONT_SIZE = 1.3f;
        private const float DEFAULT_MARGIN_OFFSET_HEIGHT = 0.15f;
        private const float DEFAULT_MARGIN_OFFSET_WIDTH = 0.2f;
        private const float DEFAULT_BUBBLE_MARGIN_OFFSET_WIDTH = 0.4f;
        private const float DEFAULT_BUBBLE_MARGIN_OFFSET_HEIGHT = 0.6f;
        private const float DEFAULT_BUBBLE_ANIMATION_IN_DURATION = 0.5f;
        private const float DEFAULT_BUBBLE_ANIMATION_OUT_DURATION = 0.35f;
        private const float DEFAULT_SINGLE_EMOJI_EXTRA_HEIGHT = 0.1f;
        private const float DEFAULT_SINGLE_EMOJI_SIZE = 3.5f;
        private const int DEFAULT_OPACITY_MAX_DISTANCE = 20;
        private const int DEFAULT_ADDITIONAL_MS_PER_CHARACTER = 20;
        private const int DEFAULT_BUBBLE_IDLE_TIME_MS = 5000;
        private const string WALLET_ID_OPENING_STYLE = "<color=#FFFFFF66><font=\"LiberationSans SDF\">";
        private const string WALLET_ID_CLOSING_STYLE = "</font></color>";

        private static readonly Regex SINGLE_EMOJI_REGEX = new (@"^\s*\\U[0-9a-fA-F]{8}\s*$", RegexOptions.Compiled);

        [field: SerializeField] public TMP_Text Username { get; private set; }
        [field: SerializeField] public SpriteRenderer BackgroundSprite { get; private set; }
        [field: SerializeField] internal RectTransform verifiedIcon { get; private set; }
        [field: SerializeField] internal SpriteRenderer verifiedIconRenderer { get; private set; }
        [field: SerializeField] internal TMP_Text messageContent { get; private set; }
        [field: SerializeField] internal SpriteRenderer mentionBackgroundSprite { get; private set; }
        [field: SerializeField] internal SpriteRenderer bubbleTailSprite { get; private set; }
        [field: SerializeField] internal RectTransform messageContentRectTransform { get; private set; }
        [field: SerializeField] internal AnimationCurve backgroundEaseAnimationCurve { get; private set; }
        [field: SerializeField] internal AnimationCurve alphaOverDistanceCurve { get; private set; }
        [field: SerializeField] internal float maxWidth { get; private set; }
        [field: SerializeField] internal Color mentionedPeakColor { get; private set; }
        [field: SerializeField] internal Color defaultPeakColor { get; private set; }

        [NonSerialized] public string Id = string.Empty;

         public float NameTagAlpha { private set; get; }

        private readonly Color finishColor = new (1, 1, 1, 0);
        private readonly Color startingTextColor = new (1, 1, 1, 0);

        private bool isAnimatingIn;
        private bool isWaiting;
        private bool isClaimedName;
        private Vector2 messageContentAnchoredPosition;
        private Vector2 usernameFinalPosition;
        private Vector2 verifiedIconFinalPosition;
        private Vector2 verifiedIconInitialPosition;
        private Vector2 preferredSize;
        private Vector2 backgroundFinalSize;
        private Vector2 textContentInitialPosition;
        private Vector2 usernamePos;
        private float previousDistance;
        private float additionalHeight;
        private Color textColor = new (1, 1, 1, 1);
        private Color usernameTextColor = new (1, 1, 1, 1);
        private Color backgroundColor = new (1, 1, 1, 1);

        private ChatBubbleConfigurationSO? chatBubbleConfiguration;
        private CancellationTokenSource? cts;

        public void InjectConfiguration(ChatBubbleConfigurationSO chatBubbleConfigurationSo)
        {
            chatBubbleConfiguration = chatBubbleConfigurationSo;
            messageContentAnchoredPosition = new Vector2(0, chatBubbleConfiguration.bubbleMarginOffsetHeight / 3);
        }

        public bool IsName(string username, string? walletId, bool hasClaimedName)
        {
            // Small performance improvement to prevent to build the name for a valid comparison
            if (!Username.text.StartsWith(username)) return false;
            return Username.text == BuildName(username, walletId, hasClaimedName);
        }

        public void SetUsername(string username, string? walletId, bool hasClaimedName, bool useVerifiedIcon)
        {
            ResetElement();

            cts.SafeCancelAndDispose();
            cts = new CancellationTokenSource();

            isClaimedName = hasClaimedName;
            verifiedIcon.gameObject.SetActive(hasClaimedName && useVerifiedIcon);

            Username.text = BuildName(username, walletId, hasClaimedName);

            Username.rectTransform.sizeDelta = new Vector2(Username.preferredWidth, DEFAULT_HEIGHT);
            messageContent.color = startingTextColor;

            float nametagMarginOffsetHeight = chatBubbleConfiguration?.nametagMarginOffsetHeight ?? DEFAULT_MARGIN_OFFSET_HEIGHT;
            float nametagMarginOffsetWidth = chatBubbleConfiguration?.nametagMarginOffsetWidth ?? DEFAULT_MARGIN_OFFSET_WIDTH;

            if (hasClaimedName && useVerifiedIcon)
            {
                usernamePos.x = Username.rectTransform.anchoredPosition.x;
                verifiedIconInitialPosition = new Vector2(Username.rectTransform.anchoredPosition.x + (Username.preferredWidth / 2) + (verifiedIcon.sizeDelta.x / 2) - (nametagMarginOffsetHeight / 2), 0);
                verifiedIcon.anchoredPosition = verifiedIconInitialPosition;
                usernamePos.x -= verifiedIcon.sizeDelta.x / 2;
                Username.rectTransform.anchoredPosition = usernamePos;
                BackgroundSprite.size = new Vector2(Username.preferredWidth + nametagMarginOffsetWidth + verifiedIcon.sizeDelta.x, Username.preferredHeight + nametagMarginOffsetHeight);
                mentionBackgroundSprite.size = new Vector2(Username.preferredWidth + nametagMarginOffsetWidth + verifiedIcon.sizeDelta.x, Username.preferredHeight + nametagMarginOffsetHeight);
            }
            else
            {
                Username.rectTransform.anchoredPosition = Vector2.zero;
                BackgroundSprite.size = new Vector2(Username.preferredWidth + nametagMarginOffsetWidth, Username.preferredHeight + nametagMarginOffsetHeight);
                mentionBackgroundSprite.size = new Vector2(Username.preferredWidth + nametagMarginOffsetWidth, Username.preferredHeight + nametagMarginOffsetHeight);
            }
        }

        public void SetTransparency(float distance, float maxDistance)
        {
            if (Math.Abs(distance - previousDistance) < DISTANCE_THRESHOLD)
                return;

            float fullOpacityMaxDistance = chatBubbleConfiguration?.fullOpacityMaxDistance ?? DEFAULT_OPACITY_MAX_DISTANCE;

            previousDistance = distance;
            usernameTextColor = Username.color;
            NameTagAlpha = alphaOverDistanceCurve.Evaluate((distance - fullOpacityMaxDistance) / (maxDistance - fullOpacityMaxDistance));
            textColor.a = distance > fullOpacityMaxDistance ? NameTagAlpha : 1;
            usernameTextColor.a = distance > fullOpacityMaxDistance ? NameTagAlpha : 1;
            backgroundColor = BackgroundSprite.color;
            backgroundColor.a = distance > fullOpacityMaxDistance ? NameTagAlpha : 1;
            var bubblePeakColor = bubbleTailSprite.color;
            bubblePeakColor.a = distance > fullOpacityMaxDistance ? NameTagAlpha : 1;
            bubbleTailSprite.color = bubblePeakColor;
            BackgroundSprite.color = backgroundColor;
            mentionBackgroundSprite.color = backgroundColor;
            Username.color = usernameTextColor;
            verifiedIconRenderer.color = backgroundColor;
        }

        public void SetChatMessage(string chatMessage, bool isMention)
        {
            cts.SafeCancelAndDispose();
            cts = new CancellationTokenSource();

            StartChatBubbleFlowAsync(chatMessage, isMention, cts.Token).Forget();
        }

        private void ResetElement()
        {
            preferredSize = Vector2.zero;
            backgroundFinalSize = Vector2.zero;
            textContentInitialPosition = Vector2.zero;
            usernamePos = Vector2.zero;
            Username.text = string.Empty;
            Username.rectTransform.anchoredPosition = Vector2.zero;
            messageContent.text = string.Empty;
            BackgroundSprite.size = Vector2.zero;
            mentionBackgroundSprite.size = Vector2.zero;
            previousDistance = 0;
            mentionBackgroundSprite.gameObject.SetActive(false);
        }

        private async UniTaskVoid StartChatBubbleFlowAsync(string chatMessage, bool isMention, CancellationToken ct)
        {
            try
            {
                if (isAnimatingIn || isWaiting)
                    await AnimateOutAsync(ct);

                await AnimateInAsync(chatMessage, isMention, ct);

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
        private async UniTask AnimateInAsync(string messageContent, bool isMention, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();

            SetHeightAndTextStyle(messageContent);
            isAnimatingIn = true;
            this.messageContent.gameObject.SetActive(true);
            bubbleTailSprite.gameObject.SetActive(true);
            bubbleTailSprite.color = isMention? mentionedPeakColor : defaultPeakColor;
            BackgroundSprite.gameObject.SetActive(!isMention);
            mentionBackgroundSprite.gameObject.SetActive(isMention);
            BackgroundSprite.color = backgroundColor;

            //Set message content and calculate the preferred size of the background with the addition of a margin
            this.messageContent.text = messageContent;

            //Force mesh is needed otherwise entryText.GetParsedText() in CalculatePreferredWidth will return the original text
            //of the previous frame
            this.messageContent.ForceMeshUpdate();

            //Calculate message content preferred size with fixed width
            preferredSize = this.messageContent.GetPreferredValues(messageContent, maxWidth, 0);
            preferredSize.x = CalculatePreferredWidth(messageContent);
            preferredSize.y += additionalHeight;
            messageContentRectTransform.sizeDelta = preferredSize;

            //Calculate the initial message content position to animate after
            textContentInitialPosition.x = preferredSize.x / 2;
            textContentInitialPosition.y = -preferredSize.y;
            messageContentRectTransform.anchoredPosition = textContentInitialPosition;

            float bubbleMarginOffsetWidth = chatBubbleConfiguration?.bubbleMarginOffsetWidth ?? DEFAULT_BUBBLE_MARGIN_OFFSET_WIDTH;
            float bubbleMarginOffsetHeight = chatBubbleConfiguration?.bubbleMarginOffsetHeight ?? DEFAULT_BUBBLE_MARGIN_OFFSET_HEIGHT;
            float animationInDuration = chatBubbleConfiguration?.animationInDuration ?? DEFAULT_BUBBLE_ANIMATION_IN_DURATION;

            preferredSize.x = messageContentRectTransform.sizeDelta.x + bubbleMarginOffsetWidth;
            preferredSize.y += bubbleMarginOffsetHeight;

            //set the username final position based on previous calculations
            usernameFinalPosition.x = (-preferredSize.x / 2) + (Username.preferredWidth / 2) + (bubbleMarginOffsetWidth / 2);
            usernameFinalPosition.y = messageContentRectTransform.sizeDelta.y + (bubbleMarginOffsetHeight / 3);

            if (isClaimedName)
            {
                verifiedIconFinalPosition.x = usernameFinalPosition.x + (Username.preferredWidth / 2) + (verifiedIcon.sizeDelta.x / 2);
                verifiedIconFinalPosition.y = usernameFinalPosition.y;
                verifiedIcon.DOAnchorPos(verifiedIconFinalPosition, animationInDuration).SetEase(backgroundEaseAnimationCurve).ToUniTask(cancellationToken: ct);
            }

            //Start all animations
            await UniTask.WhenAll(
                DOTween.Sequence().AppendInterval(animationInDuration / 3).Append(this.messageContent.DOColor(textColor, animationInDuration / 4)).Play().ToUniTask(cancellationToken: ct),
                Username.rectTransform.DOAnchorPos(usernameFinalPosition, animationInDuration).SetEase(backgroundEaseAnimationCurve).ToUniTask(cancellationToken: ct),
                this.messageContent.rectTransform.DOAnchorPos(messageContentAnchoredPosition, animationInDuration).SetEase(backgroundEaseAnimationCurve).ToUniTask(cancellationToken: ct),
                DOTween.To(() => BackgroundSprite.size, x => BackgroundSprite.size = x, preferredSize, animationInDuration).SetEase(backgroundEaseAnimationCurve).ToUniTask(cancellationToken: ct),
                DOTween.To(() => mentionBackgroundSprite.size, x => mentionBackgroundSprite.size = x, preferredSize, animationInDuration).SetEase(backgroundEaseAnimationCurve).ToUniTask(cancellationToken: ct)
            );
        }

        private void SetHeightAndTextStyle(string message)
        {
            if (SINGLE_EMOJI_REGEX.Match(message).Success)
            {
                additionalHeight = chatBubbleConfiguration?.singleEmojiExtraHeight ?? DEFAULT_SINGLE_EMOJI_EXTRA_HEIGHT;
                this.messageContent.fontSize = chatBubbleConfiguration?.singleEmojiSize ?? DEFAULT_SINGLE_EMOJI_SIZE;
                this.messageContent.alignment = TextAlignmentOptions.Center;
            }
            else
            {
                additionalHeight = 0;
                this.messageContent.fontSize = MESSAGE_CONTENT_FONT_SIZE;
                this.messageContent.alignment = TextAlignmentOptions.Left;
            }
        }

        private async UniTask AnimateOutAsync(CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();

            bubbleTailSprite.gameObject.SetActive(false);

            backgroundFinalSize.y = Username.preferredHeight + (chatBubbleConfiguration?.nametagMarginOffsetHeight ?? DEFAULT_MARGIN_OFFSET_HEIGHT);

            float nametagMarginOffsetWidth = chatBubbleConfiguration?.nametagMarginOffsetWidth ?? DEFAULT_MARGIN_OFFSET_WIDTH;
            float animationOutDuration = chatBubbleConfiguration?.animationOutDuration ?? DEFAULT_BUBBLE_ANIMATION_OUT_DURATION;

            if (isClaimedName)
            {
                backgroundFinalSize.x = Username.preferredWidth + nametagMarginOffsetWidth + verifiedIcon.sizeDelta.x;
                Username.rectTransform.DOAnchorPos(new Vector2(-verifiedIcon.sizeDelta.x / 2, 0), animationOutDuration / 2).SetEase(Ease.Linear).ToUniTask(cancellationToken: ct);
                verifiedIcon.DOAnchorPos(verifiedIconInitialPosition, animationOutDuration / 2).SetEase(Ease.Linear).ToUniTask(cancellationToken: ct);
            }
            else
            {
                backgroundFinalSize.x = Username.preferredWidth + nametagMarginOffsetWidth;
                Username.rectTransform.DOAnchorPos(Vector2.zero, animationOutDuration / 2).SetEase(Ease.Linear).ToUniTask(cancellationToken: ct);
            }

            await UniTask.WhenAll(
                messageContent.rectTransform.DOAnchorPos(textContentInitialPosition, animationOutDuration / 2).SetEase(Ease.Linear).ToUniTask(cancellationToken: ct),
                messageContent.DOColor(finishColor, animationOutDuration / 10).ToUniTask(cancellationToken: ct),
                DOTween.To(() => BackgroundSprite.size, x => BackgroundSprite.size = x, backgroundFinalSize, animationOutDuration / 2).SetEase(Ease.Linear).ToUniTask(cancellationToken: ct),
                DOTween.To(() => mentionBackgroundSprite.size, x => mentionBackgroundSprite.size = x, backgroundFinalSize, animationOutDuration / 2).SetEase(Ease.Linear).ToUniTask(cancellationToken: ct)
            );

            BackgroundSprite.gameObject.SetActive(true);
            mentionBackgroundSprite.gameObject.SetActive(false);
        }

        private float CalculatePreferredWidth(string messageContent)
        {
            float nametagMarginOffsetWidth = chatBubbleConfiguration?.nametagMarginOffsetWidth ?? DEFAULT_MARGIN_OFFSET_WIDTH;

            if (Username.preferredWidth + nametagMarginOffsetWidth + (isClaimedName ? verifiedIcon.sizeDelta.x : 0) > this.messageContent.preferredWidth)
                return Username.preferredWidth + nametagMarginOffsetWidth + (isClaimedName ? verifiedIcon.sizeDelta.x : 0);

            if (this.messageContent.GetPreferredValues(messageContent, maxWidth, 0).x < maxWidth)
                return this.messageContent.GetPreferredValues(messageContent, maxWidth, 0).x;

            return maxWidth;
        }

        private string BuildName(string username, string? walletId, bool hasClaimedName) =>
            hasClaimedName ? username : $"{username}{WALLET_ID_OPENING_STYLE}{walletId}{WALLET_ID_CLOSING_STYLE}";
    }
}
