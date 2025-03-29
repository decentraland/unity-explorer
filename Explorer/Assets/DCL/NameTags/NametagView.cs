using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using Vector2 = UnityEngine.Vector2;
using DG.Tweening;
using System;
using System.Threading;
using Utility;
using Unity.Mathematics;
using Unity.Burst;
using Unity.Collections;
using UnityEngine.Rendering;

namespace DCL.Nametags
{
    public class NametagView : MonoBehaviour
    {
        private static NativeArray<Keyframe> alphaCurveKeysNative;
        private static AnimationCurveEvaluator curveEvaluator;
        private static bool isInitialized;
        private static float verifiedIconWidth;
        private static float privateMessageIconWidth;
        private static Material opaqueMaterial;
        private static Material transparentMaterial;
        private static Vector2 claimedNameInitialPosition;
        private static readonly Vector2 ZERO_VECTOR = Vector2.zero;
        private static Vector2 messageContentAnchoredPosition;

        private int bubbleIdleTime = NametagViewConstants.DEFAULT_BUBBLE_IDLE_TIME_MS;
        private int additionalMsPerCharacter = NametagViewConstants.DEFAULT_ADDITIONAL_MS_PER_CHARACTER;
        private float animationInDuration = NametagViewConstants.DEFAULT_BUBBLE_ANIMATION_IN_DURATION;
        private float animationOutDuration = NametagViewConstants.DEFAULT_BUBBLE_ANIMATION_OUT_DURATION;
        private float nametagMarginOffsetHeight = NametagViewConstants.DEFAULT_MARGIN_OFFSET_HEIGHT;
        private float nametagMarginOffsetWidth = NametagViewConstants.DEFAULT_MARGIN_OFFSET_WIDTH;
        private float singleEmojiExtraHeight = NametagViewConstants.DEFAULT_SINGLE_EMOJI_EXTRA_HEIGHT;
        private float singleEmojiSize = NametagViewConstants.DEFAULT_SINGLE_EMOJI_SIZE;
        private float fullOpacityMaxDistance = NametagViewConstants.DEFAULT_OPACITY_MAX_DISTANCE;
        private float bubbleMarginOffsetHeight = NametagViewConstants.DEFAULT_BUBBLE_MARGIN_OFFSET_HEIGHT;
        private float bubbleMarginOffsetWidth = NametagViewConstants.DEFAULT_BUBBLE_MARGIN_OFFSET_WIDTH;

        [field: SerializeField] public SpriteRenderer BackgroundSprite { get; private set; }
        [field: SerializeField] internal TMP_Text usernameText { get; private set; }
        [field: SerializeField] internal RectTransform verifiedIcon { get; private set; }
        [field: SerializeField] internal SpriteRenderer verifiedIconRenderer { get; private set; }
        [field: SerializeField] internal TMP_Text messageContent { get; private set; }
        [field: SerializeField] internal SpriteRenderer mentionBackgroundSprite { get; private set; }
        [field: SerializeField] internal SpriteRenderer bubbleTailSprite { get; private set; }
        [field: SerializeField] internal RectTransform messageContentRectTransform { get; private set; }
        [field: SerializeField] internal AnimationCurve backgroundEaseAnimationCurve { get; private set; }
        [field: SerializeField] internal AnimationCurve alphaOverDistanceCurve { get; private set; }
        [field: SerializeField] internal RectTransform privateMessageIcon { get; private set; }
        [field: SerializeField] internal SpriteRenderer privateMessageIconRenderer { get; private set; }
        [field: SerializeField] internal TMP_Text privateMessageText { get; private set; }

        private enum AnimationState
        {
            ANIMATING,
            IDLE
        }

        private AnimationState animationState = AnimationState.IDLE;

        private bool isClaimedName;
        private bool isMention;
        private bool isTransparent;
        private bool hasPrivateMessageIcon;
        private bool hasPrivateMessageText;

        private float previousDistance;
        private Material sharedMaterial;
        private float additionalHeight;
        private Vector2 preferredSize;
        private Vector2 textContentInitialPosition;
        private Vector2 usernameFinalPosition;
        private Vector2 usernamePos;
        private Vector2 verifiedIconFinalPosition;
        private Vector2 verifiedIconInitialPosition;
        private Vector2 backgroundFinalSize;
        private CancellationTokenSource? cts;
        private Sequence? currentSequence;
        private Color spriteColor = new (1, 1, 1,1 );
        private Color receiverNameColor;
        private float cachedUsernameWidth;

        public float NameTagAlpha { private set; get; }

        /// <summary>
        /// This value represents the last calculated Sqr Distance to the camera,
        /// we use this to avoid recalculating transparency and scale when distance hasn't changed.
        /// </summary>
        public float LastSqrDistance { get; set; } = 0f;
        public string Id { set; get; } = string.Empty;

        private void Awake()
        {
            if (isInitialized) return;

            isInitialized = true;
            alphaCurveKeysNative = new NativeArray<Keyframe>(alphaOverDistanceCurve.keys, Allocator.Persistent);
            curveEvaluator = new AnimationCurveEvaluator(alphaCurveKeysNative);

            privateMessageIconWidth = privateMessageIcon.sizeDelta.x;
            verifiedIconWidth = verifiedIcon.sizeDelta.x;

            claimedNameInitialPosition = new Vector2(-verifiedIconWidth / 2, 0);
            messageContentAnchoredPosition = new Vector2(0, bubbleMarginOffsetHeight / 3);
            opaqueMaterial = new Material(BackgroundSprite.sharedMaterial);
            transparentMaterial = new Material(BackgroundSprite.sharedMaterial);

            opaqueMaterial.SetInt(NametagViewConstants.SURFACE_PROPERTY, 0); // Opaque
            opaqueMaterial.SetInt(NametagViewConstants.SRC_BLEND_PROPERTY, (int)BlendMode.One);
            opaqueMaterial.SetInt(NametagViewConstants.DST_BLEND_PROPERTY, (int)BlendMode.Zero);
            opaqueMaterial.SetInt(NametagViewConstants.Z_WRITE_PROPERTY, 1);

            transparentMaterial.SetInt(NametagViewConstants.SURFACE_PROPERTY, 1); // Transparent
            transparentMaterial.SetInt(NametagViewConstants.SRC_BLEND_PROPERTY, (int)BlendMode.SrcAlpha);
            transparentMaterial.SetInt(NametagViewConstants.DST_BLEND_PROPERTY, (int)BlendMode.OneMinusSrcAlpha);
            transparentMaterial.SetInt(NametagViewConstants.Z_WRITE_PROPERTY, 0);

            sharedMaterial = opaqueMaterial;
            BackgroundSprite.sharedMaterial = sharedMaterial;
            mentionBackgroundSprite.sharedMaterial = sharedMaterial;
            bubbleTailSprite.sharedMaterial = sharedMaterial;
            verifiedIconRenderer.sharedMaterial = sharedMaterial;
        }

        private void OnEnable()
        {
            isTransparent = false;
            UpdateMaterialState(false);
        }

        private void OnDestroy()
        {
            currentSequence?.Kill();
            currentSequence = null;
        }

        public void InjectConfiguration(ChatBubbleConfigurationSO chatBubbleConfigurationSo)
        {
            nametagMarginOffsetHeight = chatBubbleConfigurationSo.nametagMarginOffsetHeight;
            nametagMarginOffsetWidth = chatBubbleConfigurationSo.nametagMarginOffsetWidth;
            bubbleMarginOffsetWidth = chatBubbleConfigurationSo.bubbleMarginOffsetWidth;
            bubbleMarginOffsetHeight = chatBubbleConfigurationSo.bubbleMarginOffsetHeight;
            animationInDuration = chatBubbleConfigurationSo.animationInDuration;
            animationOutDuration = chatBubbleConfigurationSo.animationOutDuration;
            fullOpacityMaxDistance = chatBubbleConfigurationSo.fullOpacityMaxDistance;
            bubbleIdleTime = chatBubbleConfigurationSo.bubbleIdleTime;
            singleEmojiExtraHeight = chatBubbleConfigurationSo.singleEmojiExtraHeight;
            singleEmojiSize = chatBubbleConfigurationSo.singleEmojiSize;
            additionalMsPerCharacter = chatBubbleConfigurationSo.additionalMsPerCharacter;
            messageContentAnchoredPosition.y = bubbleMarginOffsetHeight / 3;
        }

        public bool IsSameName(string username, string? walletId, bool hasClaimedName)
        {
            string currentText = usernameText.text;

            if (hasClaimedName)
            {
                if (currentText.Length != username.Length) return false;
                if (currentText != username) return false;
                return true;
            }

            int expectedLength = username.Length + NametagViewConstants.WALLET_ID_LENGTH;
            if (currentText.Length != expectedLength) return false;
            if (!currentText.StartsWith(username, StringComparison.Ordinal)) return false;
            return true;
        }

        public void SetUsername(string username, string? walletId, bool hasClaimedName, Color usernameColor)
        {
            ResetElement();

            cts.SafeCancelAndDispose();
            cts = new CancellationTokenSource();

            isClaimedName = hasClaimedName;
            verifiedIcon.gameObject.SetActive(hasClaimedName);

            privateMessageIcon.gameObject.SetActive(false);
            privateMessageText.gameObject.SetActive(false);

            usernameText.color = usernameColor;
            usernameText.SetText(BuildName(username, walletId, hasClaimedName));
            usernameText.rectTransform.sizeDelta = new Vector2(this.usernameText.preferredWidth, NametagViewConstants.DEFAULT_HEIGHT);
            cachedUsernameWidth = usernameText.preferredWidth;
            messageContent.color = NametagViewConstants.TRANSPARENT_COLOR;

            if (hasClaimedName)
            {
                usernamePos.x = usernameText.rectTransform.anchoredPosition.x;
                verifiedIconInitialPosition = CalculateVerifiedIconPosition(usernamePos.x, cachedUsernameWidth, verifiedIconWidth, nametagMarginOffsetHeight);
                verifiedIcon.anchoredPosition = verifiedIconInitialPosition;
                usernamePos = CalculateUsernamePosition(usernamePos.x, verifiedIconWidth);
                usernameText.rectTransform.anchoredPosition = usernamePos;

                Vector2 backgroundSize = CalculateBackgroundSize(
                    cachedUsernameWidth,
                    nametagMarginOffsetWidth,
                    verifiedIconWidth,
                    usernameText.preferredHeight,
                    nametagMarginOffsetHeight,
                    true);
                BackgroundSprite.size = backgroundSize;
                mentionBackgroundSprite.size = backgroundSize;
            }
            else
            {
                usernameText.rectTransform.anchoredPosition = Vector2.zero;

                Vector2 backgroundSize = CalculateBackgroundSize(
                    cachedUsernameWidth,
                    nametagMarginOffsetWidth,
                    0,
                    usernameText.preferredHeight,
                    nametagMarginOffsetHeight,
                    false);
                BackgroundSprite.size = backgroundSize;
                mentionBackgroundSprite.size = backgroundSize;
            }
        }

        public void SetTransparency(float distance, float maxDistance)
        {
            float alpha = CalculateTransparency(distance, maxDistance, fullOpacityMaxDistance, previousDistance, NametagViewConstants.DISTANCE_THRESHOLD, out bool shouldUpdate);

            if (!shouldUpdate)
                return;

            previousDistance = distance;
            NameTagAlpha = alpha;
            bool shouldApplyAlpha = distance > fullOpacityMaxDistance;
            float finalAlpha = shouldApplyAlpha ? NameTagAlpha : 1f;

            // Update text colors while preserving original colors
            Color originalUsernameColor = usernameText.color;
            Color originalMessageColor = messageContent.color;
            Color originalPrivateMessageColor = privateMessageText.color;

            originalUsernameColor.a = finalAlpha;
            originalMessageColor.a = finalAlpha;
            originalPrivateMessageColor.a = finalAlpha;

            messageContent.color = originalMessageColor;
            usernameText.color = originalUsernameColor;
            privateMessageText.color = originalPrivateMessageColor;

            spriteColor.a = finalAlpha;
            BackgroundSprite.color = spriteColor;
            mentionBackgroundSprite.color = spriteColor;
            bubbleTailSprite.color = spriteColor;
            verifiedIconRenderer.color = spriteColor;
            privateMessageIconRenderer.color = spriteColor;

            // Only update material state when transparency state changes
            UpdateMaterialState(finalAlpha < 1f);
        }

        public void SetChatMessage(string chatMessage, bool isMention, bool isPrivateMessage, bool isOwnMessage, string receiverValidatedName, string receiverWalletId, Color receiverNameColor)
        {
            cts.SafeCancelAndDispose();
            cts = new CancellationTokenSource();
            this.isMention = isMention;
            hasPrivateMessageIcon = isPrivateMessage;
            privateMessageIcon.gameObject.SetActive(hasPrivateMessageIcon);
            privateMessageText.gameObject.SetActive(false);
            if (isPrivateMessage)
            {
                hasPrivateMessageText = isPrivateMessage && isOwnMessage;
                privateMessageText.gameObject.SetActive(hasPrivateMessageText);
                if (hasPrivateMessageText)
                {
                    string receiverName = BuildReceiverName(receiverValidatedName, receiverWalletId, string.IsNullOrEmpty(receiverWalletId));
                    this.receiverNameColor = receiverNameColor;
                    privateMessageText.SetText(receiverName);
                    receiverNameColor.a = 0;
                    privateMessageText.color = receiverNameColor;
                    privateMessageText.rectTransform.sizeDelta = new Vector2(privateMessageText.preferredWidth, NametagViewConstants.DEFAULT_HEIGHT);
                }
            }

            StartChatBubbleFlowAsync(chatMessage, cts.Token).Forget();
        }

        private void ResetElement()
        {
            preferredSize = ZERO_VECTOR;
            backgroundFinalSize = ZERO_VECTOR;
            textContentInitialPosition = ZERO_VECTOR;
            usernamePos = ZERO_VECTOR;
            usernameText.SetText(string.Empty);
            usernameText.rectTransform.anchoredPosition = ZERO_VECTOR;
            messageContent.SetText(string.Empty);
            BackgroundSprite.size = ZERO_VECTOR;
            mentionBackgroundSprite.size = ZERO_VECTOR;
            previousDistance = 0;
            mentionBackgroundSprite.gameObject.SetActive(false);
            hasPrivateMessageIcon = false;
            hasPrivateMessageText = false;
            privateMessageIcon.gameObject.SetActive(false);
            privateMessageText.gameObject.SetActive(false);
            privateMessageText.SetText(string.Empty);
            cachedUsernameWidth = 0;
        }

        private async UniTaskVoid StartChatBubbleFlowAsync(string messageText, CancellationToken ct)
        {
            try
            {
                if (animationState == AnimationState.ANIMATING)
                    await AnimateOutAsync(ct);

                await AnimateInAsync(messageText, ct);
                await UniTask.Delay(bubbleIdleTime + AdditionalMessageVisibilityTimeMs(messageText), cancellationToken: ct);
                await AnimateOutAsync(ct);
            }
            catch (OperationCanceledException)
            {
                animationState = AnimationState.IDLE;
            }
        }

        [BurstCompile]
        private static float CalculateTransparency(float distance, float maxDistance, float fullOpacityMaxDistance, float previousDistance, float distanceThreshold, out bool shouldUpdate)
        {
            shouldUpdate = math.abs(distance - previousDistance) >= distanceThreshold;

            if (!shouldUpdate)
                return 1f;

            float normalizedDistance = (distance - fullOpacityMaxDistance) / (maxDistance - fullOpacityMaxDistance);
            return curveEvaluator.Evaluate(normalizedDistance);
        }

        private int AdditionalMessageVisibilityTimeMs(string chatMessage) =>
            CalculateMessageVisibilityTime(chatMessage.Length, additionalMsPerCharacter);

        [BurstCompile]
        private static int CalculateMessageVisibilityTime(int messageLength, int msPerCharacter) =>
            messageLength * msPerCharacter;

        private async UniTask AnimateInAsync(string messageText, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();

            animationState = AnimationState.ANIMATING;
            messageContent.gameObject.SetActive(true);
            bubbleTailSprite.gameObject.SetActive(true);
            bubbleTailSprite.color = isMention ? NametagViewConstants.MENTIONED_BUBBLE_TAIL_COLOR : NametagViewConstants.NORMAL_BUBBLE_TAIL_COLOR;
            BackgroundSprite.gameObject.SetActive(!isMention);
            mentionBackgroundSprite.gameObject.SetActive(isMention);
            BackgroundSprite.color = NametagViewConstants.DEFAULT_COLOR;

            messageContent.SetText(messageText);
            SetHeightAndTextStyle(messageText);
            messageContent.ForceMeshUpdate();

            preferredSize = CalculatePreferredSize();

            messageContentRectTransform.sizeDelta = preferredSize;

            textContentInitialPosition = CalculateMessageContentPosition(preferredSize.x, preferredSize.y);
            messageContentRectTransform.anchoredPosition = textContentInitialPosition;

            preferredSize.x += bubbleMarginOffsetWidth;
            preferredSize.y += bubbleMarginOffsetHeight;

            usernameFinalPosition = CalculateUsernameFinalPosition(preferredSize.x, usernameText.preferredWidth, bubbleMarginOffsetWidth);
            usernameFinalPosition.y = messageContentRectTransform.sizeDelta.y + NametagViewConstants.BUBBLE_MARGIN_HEIGHT_THIRD;

            currentSequence?.Kill();
            currentSequence = DOTween.Sequence();

            currentSequence.AppendInterval(NametagViewConstants.ANIMATION_IN_DURATION_THIRD)
                           .Append(this.messageContent.DOColor(NametagViewConstants.DEFAULT_COLOR, NametagViewConstants.ANIMATION_IN_DURATION_QUARTER));

            if (isClaimedName)
            {
                verifiedIconFinalPosition = CalculateVerifiedIconFinalPosition(usernameFinalPosition.x, cachedUsernameWidth, verifiedIconWidth);
                verifiedIconFinalPosition.y = usernameFinalPosition.y;
                currentSequence.Join(verifiedIcon.DOAnchorPos(verifiedIconFinalPosition, animationInDuration).SetEase(backgroundEaseAnimationCurve));
            }

            if (hasPrivateMessageIcon)
            {
                Vector2 privateMessageFinalPosition = CalculatePrivateMessageIconPosition(
                    usernameFinalPosition.x,
                    cachedUsernameWidth,
                    verifiedIconWidth,
                    isClaimedName
                );

                privateMessageFinalPosition.y = usernameFinalPosition.y;
                currentSequence.Join(privateMessageIcon.DOAnchorPos(privateMessageFinalPosition, animationInDuration).SetEase(backgroundEaseAnimationCurve))
                               .Join(privateMessageIconRenderer.DOColor(NametagViewConstants.DEFAULT_COLOR, NametagViewConstants.ANIMATION_IN_DURATION_QUARTER));

                if (hasPrivateMessageText)
                {
                    Vector2 privateMessageTextFinalPosition = CalculatePrivateMessageTextPosition(
                        privateMessageFinalPosition.x,
                        privateMessageIconWidth
                    );
                    privateMessageTextFinalPosition.y = usernameFinalPosition.y;
                    currentSequence.Join(privateMessageText.rectTransform.DOAnchorPos(privateMessageTextFinalPosition, animationInDuration).SetEase(backgroundEaseAnimationCurve))
                                   .Join(privateMessageText.DOColor(receiverNameColor, NametagViewConstants.ANIMATION_IN_DURATION_QUARTER));
                }
            }

            currentSequence.Join(usernameText.rectTransform.DOAnchorPos(usernameFinalPosition, animationInDuration).SetEase(backgroundEaseAnimationCurve))
                           .Join(this.messageContent.rectTransform.DOAnchorPos(messageContentAnchoredPosition, animationInDuration).SetEase(backgroundEaseAnimationCurve));

            if (isMention)
                currentSequence.Join(DOTween.To(() => mentionBackgroundSprite.size, x => mentionBackgroundSprite.size = x, preferredSize, animationInDuration).SetEase(backgroundEaseAnimationCurve));
            else
                currentSequence.Join(DOTween.To(() => BackgroundSprite.size, x => BackgroundSprite.size = x, preferredSize, animationInDuration).SetEase(backgroundEaseAnimationCurve));

            await currentSequence.Play().ToUniTask(cancellationToken: ct);
        }

        private void SetHeightAndTextStyle(string message)
        {
            bool isSingleEmoji = NametagViewConstants.SINGLE_EMOJI_REGEX.Match(message).Success;
            if (isSingleEmoji)
            {
                additionalHeight = singleEmojiExtraHeight;
                messageContent.fontSize = singleEmojiSize;
                messageContent.alignment = TextAlignmentOptions.Center;
            }
            else
            {
                additionalHeight = 0;
                messageContent.fontSize = NametagViewConstants.MESSAGE_CONTENT_FONT_SIZE;
                messageContent.alignment = TextAlignmentOptions.Left;
            }
        }

        private async UniTask AnimateOutAsync(CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();

            bubbleTailSprite.gameObject.SetActive(false);

            backgroundFinalSize = CalculateBackgroundSize(
                cachedUsernameWidth,
                nametagMarginOffsetWidth,
                verifiedIconWidth,
                usernameText.preferredHeight,
                nametagMarginOffsetHeight,
                isClaimedName
            );

            currentSequence?.Kill();
            currentSequence = DOTween.Sequence();

            if (isClaimedName)
            {
                currentSequence.Join(usernameText.rectTransform.DOAnchorPos(claimedNameInitialPosition, NametagViewConstants.ANIMATION_OUT_DURATION_HALF).SetEase(Ease.Linear));
                currentSequence.Join(verifiedIcon.DOAnchorPos(verifiedIconInitialPosition, NametagViewConstants.ANIMATION_OUT_DURATION_HALF).SetEase(Ease.Linear));
            }
            else
                currentSequence.Join(usernameText.rectTransform.DOAnchorPos(ZERO_VECTOR, NametagViewConstants.ANIMATION_OUT_DURATION_HALF).SetEase(Ease.Linear));

            if (hasPrivateMessageIcon)
            {
                currentSequence.Join(privateMessageIcon.DOAnchorPos(ZERO_VECTOR, NametagViewConstants.ANIMATION_OUT_DURATION_HALF).SetEase(Ease.Linear))
                               .Join(privateMessageIconRenderer.DOColor(NametagViewConstants.TRANSPARENT_COLOR, NametagViewConstants.ANIMATION_OUT_DURATION_TENTH));

                if (hasPrivateMessageText)
                    currentSequence.Join(privateMessageText.rectTransform.DOAnchorPos(ZERO_VECTOR, NametagViewConstants.ANIMATION_OUT_DURATION_HALF).SetEase(Ease.Linear))
                                   .Join(privateMessageText.DOColor(NametagViewConstants.TRANSPARENT_COLOR, NametagViewConstants.ANIMATION_OUT_DURATION_TENTH));
            }

            currentSequence.Join(messageContent.rectTransform.DOAnchorPos(textContentInitialPosition, NametagViewConstants.ANIMATION_OUT_DURATION_HALF).SetEase(Ease.Linear))
                           .Join(messageContent.DOColor(NametagViewConstants.TRANSPARENT_COLOR, NametagViewConstants.ANIMATION_OUT_DURATION_TENTH));

            if (isMention)
                currentSequence.Join(DOTween.To(() => mentionBackgroundSprite.size, x => mentionBackgroundSprite.size = x, backgroundFinalSize, animationOutDuration / 2).SetEase(Ease.Linear));
            else
                currentSequence.Join(DOTween.To(() => BackgroundSprite.size, x => BackgroundSprite.size = x, backgroundFinalSize, animationOutDuration / 2).SetEase(Ease.Linear));

            await currentSequence.Play().ToUniTask(cancellationToken: ct);

            privateMessageIcon.gameObject.SetActive(false);
            privateMessageText.gameObject.SetActive(false);
            messageContent.gameObject.SetActive(false);
            BackgroundSprite.gameObject.SetActive(true);
            mentionBackgroundSprite.gameObject.SetActive(false);
        }

        private Vector2 CalculatePreferredSize() =>
            CalculatePreferredSize(preferredSize, cachedUsernameWidth, nametagMarginOffsetWidth, verifiedIconWidth, messageContent.preferredWidth, NametagViewConstants.MAX_BUBBLE_WIDTH,
                additionalHeight, messageContent.preferredHeight, isClaimedName, hasPrivateMessageIcon,
                privateMessageText.preferredWidth, privateMessageIconWidth);

        [BurstCompile]
        private static float2 CalculatePreferredSize(float2 preferredSize, float usernameWidth, float nametagMarginWidth, float verifiedIconWidth, float messageWidth, float maxWidth,
            float additionalHeight, float preferredHeight, bool isClaimedName, bool hasPrivateMessageIcon, float privateMessageTextWidth, float privateMessageIconWidth)
        {
            float baseWidth = usernameWidth + nametagMarginWidth + (isClaimedName ? verifiedIconWidth : 0) + (hasPrivateMessageIcon ? privateMessageTextWidth + privateMessageIconWidth : 0);
            float width = math.min(math.max(baseWidth, messageWidth), maxWidth);
            float height = preferredHeight + additionalHeight;
            preferredSize.x = width;
            preferredSize.y = height;
            return preferredSize;
        }

        private void UpdateMaterialState(bool transparent)
        {
            if (isTransparent == transparent)
                return;

            isTransparent = transparent;
            sharedMaterial = transparent ? transparentMaterial : opaqueMaterial;
        }

        [BurstCompile]
        private static float2 CalculateBackgroundSize(float usernameWidth, float nametagMarginWidth, float verifiedIconWidth, float usernameHeight, float nametagMarginHeight,
            bool isClaimedName)
        {
            float width = usernameWidth + nametagMarginWidth + (isClaimedName ? verifiedIconWidth : 0);
            float height = usernameHeight + nametagMarginHeight;
            return new float2(width, height);
        }

        [BurstCompile]
        private static float2 CalculateVerifiedIconPosition(float usernamePositionX, float usernameWidth, float verifiedIconWidth, float nametagMarginHeight) =>
            new float2(
                usernamePositionX + (usernameWidth / 2) + (verifiedIconWidth / 2) - (nametagMarginHeight / 2),
                0
            );

        [BurstCompile]
        private static float2 CalculateUsernamePosition(float usernamePositionX, float verifiedIconWidth) =>
            new float2(usernamePositionX - (verifiedIconWidth / 2), 0);

        [BurstCompile]
        private static float2 CalculateMessageContentPosition(float preferredSizeX, float preferredSizeY) =>
            new (preferredSizeX / 2, -preferredSizeY);

        [BurstCompile]
        private static float2 CalculateUsernameFinalPosition(float preferredSizeX, float usernameWidth, float bubbleMarginWidth) =>
            new ((-preferredSizeX / 2) + (usernameWidth / 2) + (bubbleMarginWidth / 2), 0);

        [BurstCompile]
        private static float2 CalculateVerifiedIconFinalPosition(float usernameFinalPositionX, float usernameWidth, float verifiedIconWidth) =>
            new (usernameFinalPositionX + (usernameWidth / 2) + (verifiedIconWidth / 2), 0);

        [BurstCompile]
        private static float2 CalculatePrivateMessageIconPosition(float usernameFinalPositionX, float usernameWidth, float verifiedIconWidth, bool isClaimedName) =>
            new (usernameFinalPositionX + (usernameWidth / 2) + (isClaimedName ? verifiedIconWidth : 0), 0);

        [BurstCompile]
        private static float2 CalculatePrivateMessageTextPosition(float privateMessageIconPosition, float privateIconWidth) =>
            new (privateMessageIconPosition + privateIconWidth, 0);

        [BurstCompile]
        private readonly struct AnimationCurveEvaluator
        {
            private readonly NativeArray<Keyframe> keys;

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

                for (var i = 0; i < keys.Length - 1; i++)
                {
                    if (time < keys[i + 1].time)
                    {
                        float t = (time - keys[i].time) / (keys[i + 1].time - keys[i].time);
                        return math.lerp(keys[i].value, keys[i + 1].value, t);
                    }
                }

                return keys[^1].value;
            }
        }

        private string BuildName(string username, string? walletId, bool hasClaimedName) =>
            hasClaimedName ? username : $"{username}{NametagViewConstants.WALLET_ID_OPENING_STYLE}{walletId}{NametagViewConstants.WALLET_ID_CLOSING_STYLE}";

        private string BuildReceiverName(string username, string? walletId, bool hasClaimedName) =>
            string.Concat(NametagViewConstants.RECEIVER_NAME_START_STRING, hasClaimedName ? username : $"{username}{NametagViewConstants.WALLET_ID_OPENING_STYLE}{walletId}{NametagViewConstants.WALLET_ID_CLOSING_STYLE}");

    }
}
