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
using UnityEngine.Rendering;

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
        private static readonly int SURFACE_PROPERTY = Shader.PropertyToID("_Surface");
        private static readonly int SRC_BLEND_PROPERTY = Shader.PropertyToID("_SrcBlend");
        private static readonly int DST_BLEND_PROPERTY = Shader.PropertyToID("_DstBlend");
        private static readonly int Z_WRITE_PROPERTY = Shader.PropertyToID("_ZWrite");
        private static readonly Color FINISH_COLOR = new (1, 1, 1, 0);
        private static readonly Color STARTING_TEXT_COLOR = new (1, 1, 1, 0);
        private static readonly Vector2 ZERO_VECTOR = Vector2.zero;
        private static readonly Color BACKGROUND_COLOR = new (1, 1, 1, 1);
        private static readonly Color TEXT_COLOR = new (1, 1, 1, 1);
        private static NativeArray<Keyframe> alphaCurveKeysNative;
        private static AnimationCurveEvaluator curveEvaluator;
        private static bool isInitialized;
        private Color usernameTextColor = new (1, 1, 1, 1);

        private int bubbleIdleTime = DEFAULT_BUBBLE_IDLE_TIME_MS;
        private int additionalMsPerCharacter = DEFAULT_ADDITIONAL_MS_PER_CHARACTER;
        private float animationInDuration = DEFAULT_BUBBLE_ANIMATION_IN_DURATION;
        private float animationOutDuration = DEFAULT_BUBBLE_ANIMATION_OUT_DURATION;
        private Vector2 messageContentAnchoredPosition = new (0, DEFAULT_BUBBLE_MARGIN_OFFSET_HEIGHT / 3);
        private float nametagMarginOffsetHeight = DEFAULT_MARGIN_OFFSET_HEIGHT;
        private float nametagMarginOffsetWidth = DEFAULT_MARGIN_OFFSET_WIDTH;
        private float singleEmojiExtraHeight = DEFAULT_SINGLE_EMOJI_EXTRA_HEIGHT;
        private float singleEmojiSize = DEFAULT_SINGLE_EMOJI_SIZE;
        private float fullOpacityMaxDistance = DEFAULT_OPACITY_MAX_DISTANCE;
        private float bubbleMarginOffsetHeight = DEFAULT_BUBBLE_MARGIN_OFFSET_HEIGHT;
        private float bubbleMarginOffsetWidth = DEFAULT_BUBBLE_MARGIN_OFFSET_WIDTH;

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
        [field: SerializeField] internal float maxWidth { get; private set; }
        [field: SerializeField] internal Color mentionedPeakColor { get; private set; }
        [field: SerializeField] internal Color defaultPeakColor { get; private set; }
        [field: SerializeField] internal RectTransform privateMessageIcon { get; private set; }
        [field: SerializeField] internal SpriteRenderer privateMessageIconRenderer { get; private set; }
        [field: SerializeField] internal TMP_Text privateMessageText { get; private set; }

        private bool isAnimatingIn;
        private bool isClaimedName;
        private bool isMention;
        private bool isSingleEmoji;
        private bool isTransparent;
        private bool isWaiting;
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

        // Cached widths
        private float cachedUsernameWidth;
        private float cachedVerifiedIconWidth;
        private float cachedPrivateMessageIconWidth;

        public float NameTagAlpha { private set; get; }

        /// <summary>
        /// This value represents the last calculated Sqr Distance to the camera,
        /// we use this to avoid recalculating transparency and scale when distance hasnt changed.
        /// </summary>
        public float LastSqrDistance { get; set; } = 0f;
        public string Id { set; get; } = string.Empty;

        private void Awake()
        {
            if (isInitialized) return;

            isInitialized = true;
            alphaCurveKeysNative = new NativeArray<Keyframe>(alphaOverDistanceCurve.keys, Allocator.Persistent);
            curveEvaluator = new AnimationCurveEvaluator(alphaCurveKeysNative);
        }

        private void OnEnable()
        {
            sharedMaterial = BackgroundSprite.sharedMaterial;
            mentionBackgroundSprite.sharedMaterial = sharedMaterial;
            bubbleTailSprite.sharedMaterial = sharedMaterial;
            verifiedIconRenderer.sharedMaterial = sharedMaterial;
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

        public void SetUsername(string username, string? walletId, bool hasClaimedName, bool useVerifiedIcon, Color usernameColor)
        {
            ResetElement();

            cts.SafeCancelAndDispose();
            cts = new CancellationTokenSource();

            isClaimedName = hasClaimedName;
            verifiedIcon.gameObject.SetActive(hasClaimedName && useVerifiedIcon);

            privateMessageIcon.gameObject.SetActive(false);
            privateMessageText.gameObject.SetActive(false);

            this.usernameText.color = usernameColor;
            this.usernameText.SetText(hasClaimedName ? username : $"{username}{WALLET_ID_OPENING_STYLE}{walletId}{WALLET_ID_CLOSING_STYLE}");
            this.usernameText.rectTransform.sizeDelta = new Vector2(this.usernameText.preferredWidth, DEFAULT_HEIGHT);
            cachedUsernameWidth = this.usernameText.preferredWidth;
            messageContent.color = STARTING_TEXT_COLOR;

            if (hasClaimedName && useVerifiedIcon)
            {
                cachedVerifiedIconWidth = verifiedIcon.sizeDelta.x;
                usernamePos.x = this.usernameText.rectTransform.anchoredPosition.x;
                verifiedIconInitialPosition = CalculateVerifiedIconPosition(usernamePos.x, cachedUsernameWidth, cachedVerifiedIconWidth, nametagMarginOffsetHeight);
                verifiedIcon.anchoredPosition = verifiedIconInitialPosition;
                usernamePos = CalculateUsernamePosition(usernamePos.x, cachedVerifiedIconWidth);
                this.usernameText.rectTransform.anchoredPosition = usernamePos;

                Vector2 backgroundSize = CalculateBackgroundSize(
                    cachedUsernameWidth,
                    nametagMarginOffsetWidth,
                    cachedVerifiedIconWidth,
                    this.usernameText.preferredHeight,
                    nametagMarginOffsetHeight,
                    true);
                BackgroundSprite.size = backgroundSize;
                mentionBackgroundSprite.size = backgroundSize;
            }
            else
            {
                cachedVerifiedIconWidth = 0;
                this.usernameText.rectTransform.anchoredPosition = Vector2.zero;

                Vector2 backgroundSize = CalculateBackgroundSize(
                    cachedUsernameWidth,
                    nametagMarginOffsetWidth,
                    0,
                    this.usernameText.preferredHeight,
                    nametagMarginOffsetHeight,
                    false);
                BackgroundSprite.size = backgroundSize;
                mentionBackgroundSprite.size = backgroundSize;
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

        public void SetTransparency(float distance, float maxDistance)
        {
            float alpha = CalculateTransparency(distance, maxDistance, fullOpacityMaxDistance, previousDistance, DISTANCE_THRESHOLD, out bool shouldUpdate);

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

        public void SetChatMessage(string chatMessage, bool isMention, bool isPrivateMessage, bool isOwnMessage, string channelId)
        {
            cts.SafeCancelAndDispose();
            cts = new CancellationTokenSource();
            this.isMention = isMention;
            hasPrivateMessageIcon = isPrivateMessage;
            if (isPrivateMessage)
            {
                cachedPrivateMessageIconWidth = privateMessageIcon.sizeDelta.x;
                Vector2 privateMessageInitialPosition = CalculatePrivateMessagePosition(
                    usernamePos.x,
                    cachedUsernameWidth,
                    cachedVerifiedIconWidth,
                    cachedPrivateMessageIconWidth);

                privateMessageIcon.anchoredPosition = privateMessageInitialPosition;

                hasPrivateMessageText = isPrivateMessage && isOwnMessage;
                if (hasPrivateMessageText)
                {
                    privateMessageText.SetText(channelId); //FORMAT THIS TEXT PROPERLY
                    privateMessageText.rectTransform.sizeDelta = new Vector2(privateMessageText.preferredWidth, DEFAULT_HEIGHT);

                    Vector2 privateMessageTextInitialPosition = CalculatePrivateMessageTextPosition(
                        usernamePos.x,
                        cachedUsernameWidth,
                        cachedVerifiedIconWidth,
                        cachedPrivateMessageIconWidth,
                        privateMessageText.preferredWidth
                    );

                    privateMessageText.rectTransform.anchoredPosition = privateMessageTextInitialPosition;
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
            isSingleEmoji = false;
            hasPrivateMessageIcon = false;
            hasPrivateMessageText = false;
            privateMessageIcon.gameObject.SetActive(false);
            privateMessageText.gameObject.SetActive(false);
            privateMessageText.SetText(string.Empty);

            cachedUsernameWidth = 0;
            cachedVerifiedIconWidth = 0;
            cachedPrivateMessageIconWidth = 0;
        }

        private async UniTaskVoid StartChatBubbleFlowAsync(string messageText, CancellationToken ct)
        {
            try
            {
                if (isAnimatingIn || isWaiting)
                    await AnimateOutAsync(ct);

                await AnimateInAsync(messageText, ct);

                isAnimatingIn = false;
                isWaiting = true;

                await UniTask.Delay(bubbleIdleTime + AdditionalMessageVisibilityTimeMs(messageText), cancellationToken: ct);

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
        private static int CalculateMessageVisibilityTime(int messageLength, int msPerCharacter) =>
            messageLength * msPerCharacter;

        private int AdditionalMessageVisibilityTimeMs(string chatMessage) =>
            CalculateMessageVisibilityTime(chatMessage.Length, additionalMsPerCharacter);

        private async UniTask AnimateInAsync(string messageText, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();

            isAnimatingIn = true;
            messageContent.gameObject.SetActive(true);
            bubbleTailSprite.gameObject.SetActive(true);
            bubbleTailSprite.color = isMention ? mentionedPeakColor : defaultPeakColor;
            BackgroundSprite.gameObject.SetActive(!isMention);
            mentionBackgroundSprite.gameObject.SetActive(isMention);
            BackgroundSprite.color = BACKGROUND_COLOR;

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
            usernameFinalPosition.y = messageContentRectTransform.sizeDelta.y + (bubbleMarginOffsetHeight / 3);

            currentSequence?.Kill();
            currentSequence = DOTween.Sequence();

            currentSequence.AppendInterval(animationInDuration / 3)
                           .Append(this.messageContent.DOColor(TEXT_COLOR, animationInDuration / 4));


            if (isClaimedName)
            {
                verifiedIconFinalPosition = CalculateVerifiedIconFinalPosition(usernameFinalPosition.x, cachedUsernameWidth, cachedVerifiedIconWidth);
                verifiedIconFinalPosition.y = usernameFinalPosition.y;
                currentSequence.Join(verifiedIcon.DOAnchorPos(verifiedIconFinalPosition, animationInDuration).SetEase(backgroundEaseAnimationCurve));
            }

            if (hasPrivateMessageIcon || hasPrivateMessageText)
            {
                if (hasPrivateMessageIcon)
                {
                    Vector2 privateMessageFinalPosition = CalculatePrivateMessageFinalPosition(
                        usernameFinalPosition.x,
                        cachedUsernameWidth,
                        cachedVerifiedIconWidth,
                        cachedPrivateMessageIconWidth
                    );
                    privateMessageFinalPosition.y = usernameFinalPosition.y;
                    currentSequence.Join(privateMessageIcon.DOAnchorPos(privateMessageFinalPosition, animationInDuration).SetEase(backgroundEaseAnimationCurve))
                                   .Join(privateMessageIconRenderer.DOColor(BACKGROUND_COLOR, animationInDuration / 4));
                }

                if (hasPrivateMessageText)
                {
                    Vector2 privateMessageTextFinalPosition = CalculatePrivateMessageTextFinalPosition(
                        usernameFinalPosition.x,
                        cachedUsernameWidth,
                        cachedVerifiedIconWidth,
                        cachedPrivateMessageIconWidth,
                        privateMessageText.preferredWidth
                    );
                    privateMessageTextFinalPosition.y = usernameFinalPosition.y;
                    currentSequence.Join(privateMessageText.rectTransform.DOAnchorPos(privateMessageTextFinalPosition, animationInDuration).SetEase(backgroundEaseAnimationCurve))
                                   .Join(privateMessageText.DOColor(TEXT_COLOR, animationInDuration / 4));
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
            isSingleEmoji = SINGLE_EMOJI_REGEX.Match(message).Success;
            if (isSingleEmoji)
            {
                additionalHeight = singleEmojiExtraHeight;
                messageContent.fontSize = singleEmojiSize;
                messageContent.alignment = TextAlignmentOptions.Center;
            }
            else
            {
                additionalHeight = 0;
                messageContent.fontSize = MESSAGE_CONTENT_FONT_SIZE;
                messageContent.alignment = TextAlignmentOptions.Left;
            }
        }

        private async UniTask AnimateOutAsync(CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();

            bubbleTailSprite.gameObject.SetActive(false);

            backgroundFinalSize = CalculateBackgroundFinalSize(
                cachedUsernameWidth,
                nametagMarginOffsetWidth,
                cachedVerifiedIconWidth,
                usernameText.preferredHeight,
                nametagMarginOffsetHeight,
                isClaimedName,
                hasPrivateMessageIcon,
                hasPrivateMessageText,
                (hasPrivateMessageIcon || hasPrivateMessageText) ? privateMessageText.preferredWidth + cachedPrivateMessageIconWidth : 0
            );

            currentSequence?.Kill();
            currentSequence = DOTween.Sequence();

            if (isClaimedName)
            {
                currentSequence.Join(usernameText.rectTransform.DOAnchorPos(new Vector2(-verifiedIcon.sizeDelta.x / 2, 0), animationOutDuration / 2).SetEase(Ease.Linear));
                currentSequence.Join(verifiedIcon.DOAnchorPos(verifiedIconInitialPosition, animationOutDuration / 2).SetEase(Ease.Linear));
            }
            else
                currentSequence.Join(usernameText.rectTransform.DOAnchorPos(ZERO_VECTOR, animationOutDuration / 2).SetEase(Ease.Linear));

            if (hasPrivateMessageIcon || hasPrivateMessageText)
            {
                if (hasPrivateMessageIcon)
                {
                    currentSequence.Join(privateMessageIcon.DOAnchorPos(Vector2.zero, animationOutDuration / 2).SetEase(Ease.Linear))
                                   .Join(privateMessageIconRenderer.DOColor(FINISH_COLOR, animationOutDuration / 10));
                }

                if (hasPrivateMessageText)
                {
                    currentSequence.Join(privateMessageText.rectTransform.DOAnchorPos(Vector2.zero, animationOutDuration / 2).SetEase(Ease.Linear))
                                   .Join(privateMessageText.DOColor(FINISH_COLOR, animationOutDuration / 10));
                }
            }

            currentSequence.Join(messageContent.rectTransform.DOAnchorPos(textContentInitialPosition, animationOutDuration / 2).SetEase(Ease.Linear))
                           .Join(messageContent.DOColor(FINISH_COLOR, animationOutDuration / 10));

            if (isMention)
                currentSequence.Join(DOTween.To(() => mentionBackgroundSprite.size, x => mentionBackgroundSprite.size = x, backgroundFinalSize, animationOutDuration / 2).SetEase(Ease.Linear));
            else
                currentSequence.Join(DOTween.To(() => BackgroundSprite.size, x => BackgroundSprite.size = x, backgroundFinalSize, animationOutDuration / 2).SetEase(Ease.Linear));

            await currentSequence.Play().ToUniTask(cancellationToken: ct);

            messageContent.gameObject.SetActive(false);
            BackgroundSprite.gameObject.SetActive(true);
            mentionBackgroundSprite.gameObject.SetActive(false);
        }

        [BurstCompile]
        private static float2 CalculatePreferredSize(float2 preferredSize,
            float usernameWidth, float nametagMarginWidth,
            float verifiedIconWidth, float messageWidth, float maxWidth,
            float additionalHeight, float preferredHeight, bool isClaimedName,
            bool hasPrivateMessageIcon, bool hasPrivateMessageText, float privateMessageWidth)
        {
            float baseWidth = usernameWidth + nametagMarginWidth + (isClaimedName ? verifiedIconWidth : 0) +
                            ((hasPrivateMessageIcon || hasPrivateMessageText) ? privateMessageWidth : 0);
            float width = math.min(math.max(baseWidth, messageWidth), maxWidth);
            float height = preferredHeight + additionalHeight;
            preferredSize.x = width;
            preferredSize.y = height;
            return preferredSize;
        }

        private Vector2 CalculatePreferredSize() =>
            CalculatePreferredSize(
                preferredSize,
                cachedUsernameWidth,
                nametagMarginOffsetWidth,
                cachedVerifiedIconWidth,
                messageContent.preferredWidth,
                maxWidth,
                additionalHeight,
                messageContent.preferredHeight,
                isClaimedName,
                hasPrivateMessageIcon,
                hasPrivateMessageText,
                (hasPrivateMessageIcon || hasPrivateMessageText) ? privateMessageText.preferredWidth + cachedPrivateMessageIconWidth : 0
            );

        private void UpdateMaterialState(bool transparent)
        {
            if (isTransparent == transparent)
                return;

            isTransparent = transparent;

            if (transparent)
            {
                sharedMaterial.SetInt(SURFACE_PROPERTY, 1); // Transparent
                sharedMaterial.SetInt(SRC_BLEND_PROPERTY, (int)BlendMode.SrcAlpha);
                sharedMaterial.SetInt(DST_BLEND_PROPERTY, (int)BlendMode.OneMinusSrcAlpha);
                sharedMaterial.SetInt(Z_WRITE_PROPERTY, 0);
            }
            else
            {
                sharedMaterial.SetInt(SURFACE_PROPERTY, 0); // Opaque
                sharedMaterial.SetInt(SRC_BLEND_PROPERTY, (int)BlendMode.One);
                sharedMaterial.SetInt(DST_BLEND_PROPERTY, (int)BlendMode.Zero);
                sharedMaterial.SetInt(Z_WRITE_PROPERTY, 1);
            }
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
        private static float2 CalculatePrivateMessagePosition(float usernamePositionX, float usernameWidth, float verifiedIconWidth, float privateMessageWidth) =>
            new float2(
                usernamePositionX + usernameWidth + (verifiedIconWidth > 0 ? verifiedIconWidth : 0) + (privateMessageWidth / 2),
                0
            );

        [BurstCompile]
        private static float2 CalculateUsernamePosition(float usernamePositionX, float verifiedIconWidth) =>
            new float2(usernamePositionX - (verifiedIconWidth / 2), 0);

        [BurstCompile]
        private static float2 CalculateMessageContentPosition(float preferredSizeX, float preferredSizeY) =>
            new float2(preferredSizeX / 2, -preferredSizeY);

        [BurstCompile]
        private static float2 CalculateUsernameFinalPosition(float preferredSizeX, float usernameWidth, float bubbleMarginWidth) =>
            new float2((-preferredSizeX / 2) + (usernameWidth / 2) + (bubbleMarginWidth / 2), 0);

        [BurstCompile]
        private static float2 CalculateVerifiedIconFinalPosition(float usernameFinalPositionX, float usernameWidth, float verifiedIconWidth) =>
            new float2(usernameFinalPositionX + (usernameWidth / 2) + (verifiedIconWidth / 2), 0);

        [BurstCompile]
        private static float2 CalculatePrivateMessageFinalPosition(float usernameFinalPositionX, float usernameWidth, float verifiedIconWidth, float privateMessageWidth) =>
            new float2(usernameFinalPositionX + usernameWidth + (verifiedIconWidth > 0 ? verifiedIconWidth : 0) + (privateMessageWidth / 2), 0);

        [BurstCompile]
        private static float2 CalculatePrivateMessageTextPosition(float usernamePositionX, float usernameWidth, float verifiedIconWidth, float privateIconWidth, float privateTextWidth) =>
            new float2(
                usernamePositionX + usernameWidth + (verifiedIconWidth > 0 ? verifiedIconWidth : 0) + privateIconWidth + (privateTextWidth / 2),
                0
            );

        [BurstCompile]
        private static float2 CalculatePrivateMessageTextFinalPosition(float usernameFinalPositionX, float usernameWidth, float verifiedIconWidth, float privateIconWidth, float privateTextWidth) =>
            new float2(
                usernameFinalPositionX + usernameWidth + (verifiedIconWidth > 0 ? verifiedIconWidth : 0) + privateIconWidth + (privateTextWidth / 2),
                0
            );

        [BurstCompile]
        private static float2 CalculateBackgroundFinalSize(float usernameWidth, float nametagMarginWidth, float verifiedIconWidth, float usernameHeight, float nametagMarginHeight,
            bool isClaimedName, bool hasPrivateMessageIcon, bool hasPrivateMessageText, float privateMessageWidth)
        {
            float width = usernameWidth + nametagMarginWidth + (isClaimedName ? verifiedIconWidth : 0) +
                         ((hasPrivateMessageIcon || hasPrivateMessageText) ? privateMessageWidth : 0);
            float height = usernameHeight + nametagMarginHeight;
            return new float2(width, height);
        }

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

    }
}
