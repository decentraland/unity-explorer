using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using Vector2 = UnityEngine.Vector2;
using DG.Tweening;
using System;
using System.Text.RegularExpressions;
using System.Threading;
using Utility;
using Unity.Mathematics;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;

namespace DCL.Nametags
{
    [BurstCompile]
    public struct AnimationCurveEvaluator
    {
        private NativeArray<Keyframe> keys;

        public AnimationCurveEvaluator(NativeArray<Keyframe> keys)
        {
            this.keys = keys;
        }

        [BurstCompile]
        public float Evaluate(float time)
        {
            if (keys.Length == 0)
                return 1f;

            if (time <= 0f)
                return keys[0].value;
            if (time >= 1f)
                return keys[keys.Length - 1].value;

            for (int i = 0; i < keys.Length - 1; i++)
            {
                if (time < keys[i + 1].time)
                {
                    float t = (time - keys[i].time) / (keys[i + 1].time - keys[i].time);
                    return math.lerp(keys[i].value, keys[i + 1].value, t);
                }
            }

            return keys[keys.Length - 1].value;
        }
    }

    [BurstCompile]
    public struct TransparencyJob : IJob
    {
        public float Distance;
        public float MaxDistance;
        public float FullOpacityMaxDistance;
        public float PreviousDistance;
        public float DistanceThreshold;
        public NativeArray<Keyframe> AlphaCurveKeys;

        public NativeReference<float> NameTagAlpha;
        public NativeReference<bool> ShouldUpdate;

        public void Execute()
        {
            if (math.abs(Distance - PreviousDistance) < DistanceThreshold)
            {
                ShouldUpdate.Value = false;
                return;
            }

            ShouldUpdate.Value = true;
            float normalizedDistance = (Distance - FullOpacityMaxDistance) / (MaxDistance - FullOpacityMaxDistance);

            var curveEvaluator = new AnimationCurveEvaluator(AlphaCurveKeys);
            NameTagAlpha.Value = curveEvaluator.Evaluate(normalizedDistance);
        }
    }

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
        private readonly Vector2 zeroVector = Vector2.zero;
        private readonly Vector2 oneVector = Vector2.one;

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
        private Sequence? currentSequence;
        private TMP_TextInfo? cachedTextInfo;
        private bool isSingleEmoji;

        // Cached configuration values
        private float nametagMarginOffsetHeight;
        private float nametagMarginOffsetWidth;
        private float bubbleMarginOffsetWidth;
        private float bubbleMarginOffsetHeight;
        private float animationInDuration;
        private float animationOutDuration;
        private float fullOpacityMaxDistance;
        private int bubbleIdleTime;
        private float singleEmojiExtraHeight;
        private float singleEmojiSize;
        private int additionalMsPerCharacter;
        private bool isMention;

        private Vector2 cachedPreferredValues;
        private bool needsPreferredValuesUpdate = true;

        private NativeReference<float> nameTagAlphaNative;
        private NativeReference<bool> shouldUpdateNative;
        private TransparencyJob transparencyJob;
        private JobHandle transparencyJobHandle;
        private NativeArray<Keyframe> alphaCurveKeysNative;

        private MaterialPropertyBlock propertyBlock;
        private static readonly int ColorProperty = Shader.PropertyToID("_Color");
        private Material sharedMaterial;

        private void OnEnable()
        {
            nameTagAlphaNative = new NativeReference<float>(Allocator.Persistent);
            shouldUpdateNative = new NativeReference<bool>(Allocator.Persistent);
            alphaCurveKeysNative = new NativeArray<Keyframe>(alphaOverDistanceCurve.keys, Allocator.Persistent);
            propertyBlock = new MaterialPropertyBlock();
            
            // Get the shared material from the background sprite
            sharedMaterial = BackgroundSprite.sharedMaterial;
            
            // Ensure all sprites use the same material
            mentionBackgroundSprite.sharedMaterial = sharedMaterial;
            bubbleTailSprite.sharedMaterial = sharedMaterial;
            verifiedIconRenderer.sharedMaterial = sharedMaterial;
        }

        private void OnDisable()
        {
            if (nameTagAlphaNative.IsCreated)
                nameTagAlphaNative.Dispose();
            if (shouldUpdateNative.IsCreated)
                shouldUpdateNative.Dispose();
            if (alphaCurveKeysNative.IsCreated)
                alphaCurveKeysNative.Dispose();
            propertyBlock = null;
            sharedMaterial = null;
        }

        [BurstCompile]
        private static float CalculatePreferredWidth(float usernameWidth, float nametagMarginWidth, float verifiedIconWidth, float messageWidth, float maxWidth, bool isClaimedName)
        {
            float baseWidth = usernameWidth + nametagMarginWidth + (isClaimedName ? verifiedIconWidth : 0);
            return math.min(math.max(baseWidth, messageWidth), maxWidth);
        }

        private float CalculatePreferredWidth(string messageContent)
        {
            return CalculatePreferredWidth(
                Username.preferredWidth,
                nametagMarginOffsetWidth,
                verifiedIcon.sizeDelta.x,
                this.messageContent.preferredWidth,
                maxWidth,
                isClaimedName
            );
        }

        public void InjectConfiguration(ChatBubbleConfigurationSO chatBubbleConfigurationSo)
        {
            chatBubbleConfiguration = chatBubbleConfigurationSo;

            nametagMarginOffsetHeight = chatBubbleConfiguration?.nametagMarginOffsetHeight ?? DEFAULT_MARGIN_OFFSET_HEIGHT;
            nametagMarginOffsetWidth = chatBubbleConfiguration?.nametagMarginOffsetWidth ?? DEFAULT_MARGIN_OFFSET_WIDTH;
            bubbleMarginOffsetWidth = chatBubbleConfiguration?.bubbleMarginOffsetWidth ?? DEFAULT_BUBBLE_MARGIN_OFFSET_WIDTH;
            bubbleMarginOffsetHeight = chatBubbleConfiguration?.bubbleMarginOffsetHeight ?? DEFAULT_BUBBLE_MARGIN_OFFSET_HEIGHT;
            animationInDuration = chatBubbleConfiguration?.animationInDuration ?? DEFAULT_BUBBLE_ANIMATION_IN_DURATION;
            animationOutDuration = chatBubbleConfiguration?.animationOutDuration ?? DEFAULT_BUBBLE_ANIMATION_OUT_DURATION;
            fullOpacityMaxDistance = chatBubbleConfiguration?.fullOpacityMaxDistance ?? DEFAULT_OPACITY_MAX_DISTANCE;
            bubbleIdleTime = chatBubbleConfiguration?.bubbleIdleTime ?? DEFAULT_BUBBLE_IDLE_TIME_MS;
            singleEmojiExtraHeight = chatBubbleConfiguration?.singleEmojiExtraHeight ?? DEFAULT_SINGLE_EMOJI_EXTRA_HEIGHT;
            singleEmojiSize = chatBubbleConfiguration?.singleEmojiSize ?? DEFAULT_SINGLE_EMOJI_SIZE;
            additionalMsPerCharacter = chatBubbleConfiguration?.additionalMsPerCharacter ?? DEFAULT_ADDITIONAL_MS_PER_CHARACTER;

            messageContentAnchoredPosition = new Vector2(0, bubbleMarginOffsetHeight / 3);

        }

        [BurstCompile]
        private static Vector2 CalculateBackgroundSize(float usernameWidth, float nametagMarginWidth, float verifiedIconWidth, float usernameHeight, float nametagMarginHeight, bool isClaimedName)
        {
            float width = usernameWidth + nametagMarginWidth + (isClaimedName ? verifiedIconWidth : 0);
            float height = usernameHeight + nametagMarginHeight;
            return new Vector2(width, height);
        }

        [BurstCompile]
        private static Vector2 CalculateVerifiedIconPosition(float usernamePositionX, float usernameWidth, float verifiedIconWidth, float nametagMarginHeight)
        {
            return new Vector2(
                usernamePositionX + (usernameWidth / 2) + (verifiedIconWidth / 2) - (nametagMarginHeight / 2),
                0
            );
        }

        [BurstCompile]
        private static Vector2 CalculateUsernamePosition(float usernamePositionX, float verifiedIconWidth)
        {
            return new Vector2(usernamePositionX - verifiedIconWidth / 2, 0);
        }

        [BurstCompile]
        private static Vector2 CalculateMessageContentPosition(float preferredSizeX, float preferredSizeY)
        {
            return new Vector2(preferredSizeX / 2, -preferredSizeY);
        }

        [BurstCompile]
        private static Vector2 CalculateUsernameFinalPosition(float preferredSizeX, float usernameWidth, float bubbleMarginWidth)
        {
            return new Vector2(
                (-preferredSizeX / 2) + (usernameWidth / 2) + (bubbleMarginWidth / 2),
                0
            );
        }

        [BurstCompile]
        private static Vector2 CalculateVerifiedIconFinalPosition(float usernameFinalPositionX, float usernameWidth, float verifiedIconWidth)
        {
            return new Vector2(
                usernameFinalPositionX + (usernameWidth / 2) + (verifiedIconWidth / 2),
                0
            );
        }

        [BurstCompile]
        private static Vector2 CalculateBackgroundFinalSize(float usernameWidth, float nametagMarginWidth, float verifiedIconWidth, float usernameHeight, float nametagMarginHeight, bool isClaimedName)
        {
            float width = usernameWidth + nametagMarginWidth + (isClaimedName ? verifiedIconWidth : 0);
            float height = usernameHeight + nametagMarginHeight;
            return new Vector2(width, height);
        }

        public void SetUsername(string username, string? walletId, bool hasClaimedName, bool useVerifiedIcon)
        {
            ResetElement();

            cts.SafeCancelAndDispose();
            cts = new CancellationTokenSource();

            isClaimedName = hasClaimedName;
            verifiedIcon.gameObject.SetActive(hasClaimedName && useVerifiedIcon);

            Username.SetText(hasClaimedName ? username : $"{username}{WALLET_ID_OPENING_STYLE}{walletId}{WALLET_ID_CLOSING_STYLE}");
            Username.rectTransform.sizeDelta = new Vector2(Username.preferredWidth, DEFAULT_HEIGHT);
            messageContent.color = startingTextColor;

            if (hasClaimedName && useVerifiedIcon)
            {
                usernamePos.x = Username.rectTransform.anchoredPosition.x;
                verifiedIconInitialPosition = CalculateVerifiedIconPosition(usernamePos.x, Username.preferredWidth, verifiedIcon.sizeDelta.x, nametagMarginOffsetHeight);
                verifiedIcon.anchoredPosition = verifiedIconInitialPosition;
                usernamePos = CalculateUsernamePosition(usernamePos.x, verifiedIcon.sizeDelta.x);
                Username.rectTransform.anchoredPosition = usernamePos;
                
                Vector2 backgroundSize = CalculateBackgroundSize(Username.preferredWidth, nametagMarginOffsetWidth, verifiedIcon.sizeDelta.x, Username.preferredHeight, nametagMarginOffsetHeight, true);
                BackgroundSprite.size = backgroundSize;
                mentionBackgroundSprite.size = backgroundSize;
            }
            else
            {
                Username.rectTransform.anchoredPosition = Vector2.zero;
                Vector2 backgroundSize = CalculateBackgroundSize(Username.preferredWidth, nametagMarginOffsetWidth, 0, Username.preferredHeight, nametagMarginOffsetHeight, false);
                BackgroundSprite.size = backgroundSize;
                mentionBackgroundSprite.size = backgroundSize;
            }
        }

        [BurstCompile]
        private static float CalculateTransparency(float distance, float maxDistance, float fullOpacityMaxDistance, float previousDistance, float distanceThreshold, NativeArray<Keyframe> alphaCurveKeys, out bool shouldUpdate)
        {
            shouldUpdate = math.abs(distance - previousDistance) >= distanceThreshold;
            
            if (!shouldUpdate)
                return 1f;

            float normalizedDistance = (distance - fullOpacityMaxDistance) / (maxDistance - fullOpacityMaxDistance);
            var curveEvaluator = new AnimationCurveEvaluator(alphaCurveKeys);
            return curveEvaluator.Evaluate(normalizedDistance);
        }

        public void SetTransparency(float distance, float maxDistance)
        {
            bool shouldUpdate;
            float alpha = CalculateTransparency(distance, maxDistance, fullOpacityMaxDistance, previousDistance, DISTANCE_THRESHOLD, alphaCurveKeysNative, out shouldUpdate);

            if (!shouldUpdate)
                return;

            previousDistance = distance;
            NameTagAlpha = alpha;
            bool shouldApplyAlpha = distance > fullOpacityMaxDistance;
            float finalAlpha = shouldApplyAlpha ? NameTagAlpha : 1f;

            // Update text colors while preserving original colors
            Color originalUsernameColor = Username.color;
            Color originalMessageColor = messageContent.color;
            
            originalUsernameColor.a = finalAlpha;
            originalMessageColor.a = finalAlpha;
            
            messageContent.color = originalMessageColor;
            Username.color = originalUsernameColor;

            // Update sprite colors using direct color property
            Color spriteColor = new Color(1, 1, 1, finalAlpha);
            BackgroundSprite.color = spriteColor;
            mentionBackgroundSprite.color = spriteColor;
            bubbleTailSprite.color = spriteColor;
            verifiedIconRenderer.color = spriteColor;
        }

        public void SetChatMessage(string chatMessage, bool isMention)
        {
            cts.SafeCancelAndDispose();
            cts = new CancellationTokenSource();
            this.isMention = isMention;
            StartChatBubbleFlowAsync(chatMessage, cts.Token).Forget();
        }

        private void ResetElement()
        {
            preferredSize = zeroVector;
            backgroundFinalSize = zeroVector;
            textContentInitialPosition = zeroVector;
            usernamePos = zeroVector;
            Username.SetText(string.Empty);
            Username.rectTransform.anchoredPosition = zeroVector;
            messageContent.SetText(string.Empty);
            BackgroundSprite.size = zeroVector;
            mentionBackgroundSprite.size = zeroVector;
            previousDistance = 0;
            mentionBackgroundSprite.gameObject.SetActive(false);
            needsPreferredValuesUpdate = true;
            cachedTextInfo = null;
            isSingleEmoji = false;
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

                await UniTask.Delay(bubbleIdleTime + AdditionalMessageVisibilityTimeMs(chatMessage), cancellationToken: ct);

                isWaiting = false;

                await AnimateOutAsync(ct);
            }
            catch (OperationCanceledException)
            {
                isAnimatingIn = false;
                isWaiting = false;
            }
        }

        [BurstCompile]
        private static int CalculateMessageVisibilityTime(int messageLength, int msPerCharacter)
        {
            return messageLength * msPerCharacter;
        }

        private int AdditionalMessageVisibilityTimeMs(string chatMessage)
        {
            return CalculateMessageVisibilityTime(chatMessage.Length, additionalMsPerCharacter);
        }

        private async UniTask AnimateInAsync(string messageContent, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();

            isAnimatingIn = true;
            this.messageContent.gameObject.SetActive(true);
            bubbleTailSprite.gameObject.SetActive(true);
            bubbleTailSprite.color = isMention ? mentionedPeakColor : defaultPeakColor;
            BackgroundSprite.gameObject.SetActive(!isMention);
            mentionBackgroundSprite.gameObject.SetActive(isMention);
            BackgroundSprite.color = backgroundColor;

            this.messageContent.SetText(messageContent);
            UpdatePreferredValues(messageContent);

            // Reset and calculate preferred size
            preferredSize = zeroVector;
            preferredSize.x = CalculatePreferredWidth(messageContent);
            this.messageContent.ForceMeshUpdate();
            preferredSize.y = this.messageContent.preferredHeight + additionalHeight;
            messageContentRectTransform.sizeDelta = preferredSize;

            textContentInitialPosition = CalculateMessageContentPosition(preferredSize.x, preferredSize.y);
            messageContentRectTransform.anchoredPosition = textContentInitialPosition;

            // Calculate final size with margins
            preferredSize.x += bubbleMarginOffsetWidth;
            preferredSize.y += bubbleMarginOffsetHeight;

            usernameFinalPosition = CalculateUsernameFinalPosition(preferredSize.x, Username.preferredWidth, bubbleMarginOffsetWidth);
            usernameFinalPosition.y = messageContentRectTransform.sizeDelta.y + (bubbleMarginOffsetHeight / 3);

            if (isClaimedName)
            {
                verifiedIconFinalPosition = CalculateVerifiedIconFinalPosition(usernameFinalPosition.x, Username.preferredWidth, verifiedIcon.sizeDelta.x);
                verifiedIconFinalPosition.y = usernameFinalPosition.y;
                verifiedIcon.DOAnchorPos(verifiedIconFinalPosition, animationInDuration).SetEase(backgroundEaseAnimationCurve).ToUniTask(cancellationToken: ct);
            }

            currentSequence?.Kill();
            currentSequence = DOTween.Sequence();
            currentSequence.AppendInterval(animationInDuration / 3)
                         .Append(this.messageContent.DOColor(textColor, animationInDuration / 4))
                         .Join(Username.rectTransform.DOAnchorPos(usernameFinalPosition, animationInDuration).SetEase(backgroundEaseAnimationCurve))
                         .Join(this.messageContent.rectTransform.DOAnchorPos(messageContentAnchoredPosition, animationInDuration).SetEase(backgroundEaseAnimationCurve));

            if (isMention)
            {
                currentSequence.Join(DOTween.To(() => mentionBackgroundSprite.size, x => mentionBackgroundSprite.size = x, preferredSize, animationInDuration).SetEase(backgroundEaseAnimationCurve));
            }
            else
            {
                currentSequence.Join(DOTween.To(() => BackgroundSprite.size, x => BackgroundSprite.size = x, preferredSize, animationInDuration).SetEase(backgroundEaseAnimationCurve));
            }

            await currentSequence.Play().ToUniTask(cancellationToken: ct);
        }

        private void SetHeightAndTextStyle(string message)
        {
            if (isSingleEmoji = SINGLE_EMOJI_REGEX.Match(message).Success)
            {
                additionalHeight = singleEmojiExtraHeight;
                this.messageContent.fontSize = singleEmojiSize;
                this.messageContent.alignment = TextAlignmentOptions.Center;
            }
            else
            {
                additionalHeight = 0;
                this.messageContent.fontSize = MESSAGE_CONTENT_FONT_SIZE;
                this.messageContent.alignment = TextAlignmentOptions.Left;
            }
        }

        private void UpdatePreferredValues(string messageContent)
        {
            if (!needsPreferredValuesUpdate)
                return;

            SetHeightAndTextStyle(messageContent);
            this.messageContent.ForceMeshUpdate();
            cachedTextInfo = this.messageContent.textInfo;
            cachedPreferredValues = this.messageContent.GetPreferredValues(messageContent, maxWidth, float.PositiveInfinity);
            needsPreferredValuesUpdate = false;
        }

        private async UniTask AnimateOutAsync(CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();

            bubbleTailSprite.gameObject.SetActive(false);

            backgroundFinalSize = CalculateBackgroundFinalSize(
                Username.preferredWidth,
                nametagMarginOffsetWidth,
                verifiedIcon.sizeDelta.x,
                Username.preferredHeight,
                nametagMarginOffsetHeight,
                isClaimedName
            );

            if (isClaimedName)
            {
                Username.rectTransform.DOAnchorPos(new Vector2(-verifiedIcon.sizeDelta.x / 2, 0), animationOutDuration / 2).SetEase(Ease.Linear).ToUniTask(cancellationToken: ct);
                verifiedIcon.DOAnchorPos(verifiedIconInitialPosition, animationOutDuration / 2).SetEase(Ease.Linear).ToUniTask(cancellationToken: ct);
            }
            else
            {
                Username.rectTransform.DOAnchorPos(zeroVector, animationOutDuration / 2).SetEase(Ease.Linear).ToUniTask(cancellationToken: ct);
            }

            currentSequence?.Kill();
            currentSequence = DOTween.Sequence();
            currentSequence.Join(messageContent.rectTransform.DOAnchorPos(textContentInitialPosition, animationOutDuration / 2).SetEase(Ease.Linear))
                         .Join(messageContent.DOColor(finishColor, animationOutDuration / 10));

            if (isMention)
            {
                currentSequence.Join(DOTween.To(() => mentionBackgroundSprite.size, x => mentionBackgroundSprite.size = x, backgroundFinalSize, animationOutDuration / 2).SetEase(Ease.Linear));
            }
            else
            {
                currentSequence.Join(DOTween.To(() => BackgroundSprite.size, x => BackgroundSprite.size = x, backgroundFinalSize, animationOutDuration / 2).SetEase(Ease.Linear));
            }

            await currentSequence.Play().ToUniTask(cancellationToken: ct);

            BackgroundSprite.gameObject.SetActive(true);
            mentionBackgroundSprite.gameObject.SetActive(false);
        }

        private void OnDestroy()
        {
            currentSequence?.Kill();
            currentSequence = null;
        }
    }
}
