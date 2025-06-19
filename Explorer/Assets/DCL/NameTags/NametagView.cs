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
        private static float verifiedIconWidth;
        private static float privateMessageIconWidth;
        private static float isSpeakingIconWidth;
        private static Material? opaqueMaterial;
        private static Material? transparentMaterial;
        private static Vector2 claimedNameInitialPosition;
        private static Vector2 messageContentAnchoredPosition;

        // Note: I have made all of these static, as they are the same for all nametags - FOR NOW -
        // In case in the future we decide to change that, we just need to make these non-static so each nametag
        // loads its own configuration file independently
        private static bool isExternalConfigurationLoaded;
        private static int bubbleIdleTime = NametagViewConstants.DEFAULT_BUBBLE_IDLE_TIME_MS;
        private static int additionalMsPerCharacter = NametagViewConstants.DEFAULT_ADDITIONAL_MS_PER_CHARACTER;
        private static float animationInDuration = NametagViewConstants.DEFAULT_BUBBLE_ANIMATION_IN_DURATION;
        private static float animationOutDuration = NametagViewConstants.DEFAULT_BUBBLE_ANIMATION_OUT_DURATION;
        private static float nametagMarginOffsetHeight = NametagViewConstants.DEFAULT_MARGIN_OFFSET_HEIGHT;
        private static float nametagMarginOffsetWidth = NametagViewConstants.DEFAULT_MARGIN_OFFSET_WIDTH;
        private static float singleEmojiExtraHeight = NametagViewConstants.DEFAULT_SINGLE_EMOJI_EXTRA_HEIGHT;
        private static float singleEmojiSize = NametagViewConstants.DEFAULT_SINGLE_EMOJI_SIZE;
        private static float fullOpacityMaxDistance = NametagViewConstants.DEFAULT_OPACITY_MAX_DISTANCE;
        private static float bubbleMarginOffsetHeight = NametagViewConstants.DEFAULT_BUBBLE_MARGIN_OFFSET_HEIGHT;
        private static float bubbleMarginOffsetWidth = NametagViewConstants.DEFAULT_BUBBLE_MARGIN_OFFSET_WIDTH;
        private static float animationInDurationThird = NametagViewConstants.DEFAULT_BUBBLE_ANIMATION_IN_DURATION / 3f;
        private static float animationInDurationQuarter = NametagViewConstants.DEFAULT_BUBBLE_ANIMATION_IN_DURATION / 4f;
        private static float animationOutDurationHalf = NametagViewConstants.DEFAULT_BUBBLE_ANIMATION_OUT_DURATION / 2f;
        private static float animationOutDurationTenth = NametagViewConstants.DEFAULT_BUBBLE_ANIMATION_OUT_DURATION / 10f;
        private static float bubbleMarginOffsetHeightThird = NametagViewConstants.DEFAULT_BUBBLE_MARGIN_OFFSET_HEIGHT / 3f;

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
        [field: SerializeField] internal RectTransform isSpeakingIcon { get; private set; }
        [field: SerializeField] internal SpriteRenderer isSpeakingIconRenderer { get; private set; }
        [field: SerializeField] internal SpriteRenderer isSpeakingIconOuterRenderer { get; private set; }
        [field: SerializeField] internal RectTransform isSpeakingIconRect { get; private set; }
        [field: SerializeField] internal RectTransform isSpeakingIconOuterRect { get; private set; }

        [field: SerializeField] internal TMP_Text privateMessageText { get; private set; }

        private enum AnimationState
        {
            ANIMATING,
            IDLE
        }

        private AnimationState animationState = AnimationState.IDLE;

        private bool isClaimedName;
        private bool isUsingVerifiedIcon;
        private bool isMention;
        private bool isTransparent;
        private bool isPrivateMessage;
        private bool showPrivateMessageRecipient;
        private bool isInitialized;
        private bool isSpeaking = true;
        private Sequence? isSpeakingCurrentSequence;


        private float previousDistance;
        private Material sharedMaterial;
        private float additionalHeight;
        private Vector2 preferredSize;
        private Vector2 textContentInitialPosition;
        private Vector2 usernameFinalPosition;
        private Vector2 usernamePos;
        private Vector2 verifiedIconFinalPosition;
        private Vector2 verifiedIconInitialPosition;
        private Vector2 isSpeakingIconInitialPosition;
        private Vector2 isSpeakingIconFinalPosition;
        private Vector2 backgroundFinalSize;
        private Vector2 privateMessageFinalPosition;
        private Vector2 privateMessageTextFinalPosition;
        private Vector2 tempSizeDelta;
        private CancellationTokenSource? cts;
        private Sequence? currentSequence;
        private Sequence? isTalkingCurrentSequence;
        private Color currentSpritesColor = new (1, 1, 1,1 );
        private Color currentBubbleTailSpriteColor;
        private Color currentIsTalkingSpriteColor;
        private Color currentRecipientNameColor;
        private Color currentUsernameColor;

        private float cachedUsernameWidth;

        public float NameTagAlpha { private set; get; }

        /// <summary>
        /// This value represents the last calculated Sqr Distance to the camera,
        /// we use this to avoid recalculating transparency and scale when distance hasn't changed.
        /// </summary>
        public float LastSqrDistance { get; set; } = 0f;
        public string Id { set; get; } = string.Empty;
        public int ProfileVersion { set; get; } = -1;

        private void Awake()
        {
            if (isInitialized) return;

            isInitialized = true;
            alphaCurveKeysNative = new NativeArray<Keyframe>(alphaOverDistanceCurve.keys, Allocator.Persistent);
            curveEvaluator = new AnimationCurveEvaluator(alphaCurveKeysNative);

            privateMessageIconWidth = privateMessageIcon.sizeDelta.x;
            verifiedIconWidth = verifiedIcon.sizeDelta.x;
            isSpeakingIconWidth = isSpeakingIcon.sizeDelta.x;

            claimedNameInitialPosition = new Vector2(-(verifiedIconWidth / 2) - (isSpeaking ? isSpeakingIconWidth : 0), 0);
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
            isSpeakingIconRenderer.sharedMaterial = sharedMaterial;
            isSpeakingIconOuterRenderer.sharedMaterial = sharedMaterial;
            SetIsSpeaking(false);
        }

        private void OnEnable()
        {
            isTransparent = false;
            UpdateMaterialState(false);
        }

        private void OnDestroy()
        {
            if (currentSequence != null)
            {
                currentSequence.Kill();
                currentSequence = null;
            }
        }

        public void InjectConfiguration(ChatBubbleConfigurationSO chatBubbleConfigurationSo)
        {
            if (isExternalConfigurationLoaded) return;

            isExternalConfigurationLoaded = true;
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
            animationInDurationThird = animationInDuration / 3f;
            animationInDurationQuarter = animationInDuration / 4f;
            animationOutDurationHalf = animationOutDuration / 2f;
            animationOutDurationTenth = animationOutDuration / 10f;
            bubbleMarginOffsetHeightThird = bubbleMarginOffsetHeight / 3f;
        }

        public bool IsSameName(string username, bool hasClaimedName)
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
            return currentText.StartsWith(username, StringComparison.Ordinal);
        }

        public void SetUsername(string username, string? walletId, bool hasClaimedName, bool useVerifiedIcon, Color usernameColor)
        {
            ResetElement();

            cts.SafeCancelAndDispose();
            cts = new CancellationTokenSource();

            isClaimedName = hasClaimedName;
            isUsingVerifiedIcon = useVerifiedIcon;
            verifiedIcon.gameObject.SetActive(useVerifiedIcon);

            privateMessageIcon.gameObject.SetActive(false);
            privateMessageText.gameObject.SetActive(false);

            this.currentUsernameColor = usernameColor;
            usernameText.color = usernameColor;
            usernameText.SetText(BuildName(username, walletId, hasClaimedName));
            usernameText.rectTransform.sizeDelta = new Vector2(this.usernameText.preferredWidth, NametagViewConstants.DEFAULT_HEIGHT);
            cachedUsernameWidth = usernameText.preferredWidth;
            messageContent.color = NametagViewConstants.DEFAULT_TRANSPARENT_COLOR;

            isSpeakingIconInitialPosition = CalculateTalkingIconPosition(usernamePos.x, cachedUsernameWidth, verifiedIconWidth, hasClaimedName, nametagMarginOffsetHeight);
            isSpeakingIcon.anchoredPosition = isSpeakingIconInitialPosition;

            SetInitialPositions(hasClaimedName);
        }

        private void SetInitialPositions(bool hasClaimedName)
        {
            if (hasClaimedName && isUsingVerifiedIcon)
            {
                usernamePos.x = usernameText.rectTransform.anchoredPosition.x;
                verifiedIconInitialPosition = CalculateVerifiedIconPosition(usernamePos.x, cachedUsernameWidth, verifiedIconWidth, isSpeakingIconWidth, nametagMarginOffsetHeight, isSpeaking);
                verifiedIcon.anchoredPosition = verifiedIconInitialPosition;
                usernamePos = CalculateUsernamePosition(usernamePos.x, verifiedIconWidth, isSpeakingIconWidth, isSpeaking);
                usernameText.rectTransform.anchoredPosition = usernamePos;

                Vector2 backgroundSize = CalculateBackgroundSize(
                    cachedUsernameWidth,
                    nametagMarginOffsetWidth,
                    verifiedIconWidth,
                    usernameText.preferredHeight,
                    nametagMarginOffsetHeight,
                    isSpeakingIconWidth,
                    isSpeaking,
                    true);
                BackgroundSprite.size = backgroundSize;
                mentionBackgroundSprite.size = backgroundSize;

                if (isSpeaking)
                {
                    BackgroundSprite.transform.localPosition = new Vector3(isSpeakingIconWidth / 2, 0, 0);
                    mentionBackgroundSprite.transform.localPosition = new Vector3(isSpeakingIconWidth / 2, 0, 0);
                }
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
                    isSpeakingIconWidth,
                    isSpeaking,
                    false);
                BackgroundSprite.size = backgroundSize;
                mentionBackgroundSprite.size = backgroundSize;

                if (isSpeaking)
                {
                    BackgroundSprite.transform.localPosition = new Vector3(isSpeakingIconWidth / 2, 0, 0);
                    mentionBackgroundSprite.transform.localPosition = new Vector3(isSpeakingIconWidth / 2, 0, 0);
                }
            }
        }

        public void SetTransparency(float distance, float maxDistance)
        {
            float alpha = CalculateTransparency(distance, maxDistance, fullOpacityMaxDistance, previousDistance, NametagViewConstants.DISTANCE_THRESHOLD, out bool shouldUpdate);

            if (!shouldUpdate)
                return;

            previousDistance = distance;
            NameTagAlpha = alpha;

            // Update name text colors (as they got their own colors)
            currentRecipientNameColor.a = NameTagAlpha;
            currentUsernameColor.a = NameTagAlpha;
            usernameText.color = currentUsernameColor;
            privateMessageText.color = currentRecipientNameColor;

            // Update the bubble tail sprite color as it changes depending on mention state
            currentBubbleTailSpriteColor = bubbleTailSprite.color;
            currentBubbleTailSpriteColor.a = NameTagAlpha;
            bubbleTailSprite.color = currentBubbleTailSpriteColor;

            // Update the is talking sprite with its own color
            currentIsTalkingSpriteColor = isSpeakingIconRenderer.color;
            currentIsTalkingSpriteColor.a = NameTagAlpha;
            isSpeakingIconRenderer.color = currentIsTalkingSpriteColor;

            // Update the rest of the sprites and text that share the same color
            currentSpritesColor.a = NameTagAlpha;
            messageContent.color = currentSpritesColor;
            BackgroundSprite.color = currentSpritesColor;
            mentionBackgroundSprite.color = currentSpritesColor;
            verifiedIconRenderer.color = currentSpritesColor;
            privateMessageIconRenderer.color = currentSpritesColor;

            UpdateMaterialState(NameTagAlpha < 1f);
        }

        public void SetChatMessage(string chatMessage, bool isMention, bool isPrivateMessage, bool isOwnMessage, string recipientValidatedName, string recipientWalletId, Color recipientNameColor)
        {
            cts.SafeCancelAndDispose();
            cts = new CancellationTokenSource();
            this.isMention = isMention;
            this.isPrivateMessage = isPrivateMessage;
            privateMessageIcon.gameObject.SetActive(isPrivateMessage);
            privateMessageText.gameObject.SetActive(false);
            if (isPrivateMessage)
            {
                privateMessageIconRenderer.color = NametagViewConstants.DEFAULT_TRANSPARENT_COLOR;
                showPrivateMessageRecipient = isOwnMessage;
                privateMessageText.gameObject.SetActive(showPrivateMessageRecipient);
                if (showPrivateMessageRecipient)
                    SetPrivateMessageText(recipientValidatedName, recipientWalletId, recipientNameColor);
            }

            StartChatBubbleFlowAsync(chatMessage, cts.Token).Forget();
        }

        public void SetIsSpeaking(bool isSpeaking)
        {
            this.isSpeaking = isSpeaking;

            isSpeakingIcon.gameObject.SetActive(isSpeaking);

            if (animationState == AnimationState.IDLE)
            {
                usernameText.rectTransform.anchoredPosition = Vector2.zero;
                SetInitialPositions(isClaimedName);
            }

            if (isSpeaking)
            {
                isSpeakingCurrentSequence = DOTween.Sequence();
                isSpeakingCurrentSequence.Append(isSpeakingIconRect.DOScaleY(0.2f, animationInDuration));
                isSpeakingCurrentSequence.Join(isSpeakingIconOuterRect.DOScaleY(1, animationInDuration));
                isSpeakingCurrentSequence.Append(isSpeakingIconOuterRect.DOScaleY(0.2f, animationInDuration));
                isSpeakingCurrentSequence.Join(isSpeakingIconRect.DOScaleY(1, animationInDuration));
                isSpeakingCurrentSequence.SetLoops(-1);
                isSpeakingCurrentSequence.Play();

                BackgroundSprite.transform.localPosition = new Vector3(isSpeakingIconWidth / 2, 0, 0);
                mentionBackgroundSprite.transform.localPosition = new Vector3(isSpeakingIconWidth / 2, 0, 0);
            }
            else
            {
                isSpeakingCurrentSequence?.Kill();
                isSpeakingCurrentSequence = null;
                BackgroundSprite.transform.localPosition = Vector3.zero;
                mentionBackgroundSprite.transform.localPosition = Vector3.zero;
            }
        }

        private void ResetElement()
        {
            preferredSize = NametagViewConstants.ZERO_VECTOR;
            backgroundFinalSize = NametagViewConstants.ZERO_VECTOR;
            textContentInitialPosition = NametagViewConstants.ZERO_VECTOR;
            usernamePos = NametagViewConstants.ZERO_VECTOR;
            usernameText.SetText(string.Empty);
            usernameText.rectTransform.anchoredPosition = NametagViewConstants.ZERO_VECTOR;
            messageContent.SetText(string.Empty);
            BackgroundSprite.size = NametagViewConstants.ZERO_VECTOR;
            mentionBackgroundSprite.size = NametagViewConstants.ZERO_VECTOR;
            previousDistance = 0;
            mentionBackgroundSprite.gameObject.SetActive(false);
            isPrivateMessage = false;
            showPrivateMessageRecipient = false;
            privateMessageIcon.gameObject.SetActive(false);
            privateMessageText.gameObject.SetActive(false);
            privateMessageText.SetText(string.Empty);
            cachedUsernameWidth = 0;
            additionalHeight = 0;
            isSpeaking = false;
            isSpeakingIcon.gameObject.SetActive(false);
            isSpeakingCurrentSequence?.Kill();
            isSpeakingCurrentSequence = null;
            BackgroundSprite.transform.localPosition = Vector3.zero;
            mentionBackgroundSprite.transform.localPosition = Vector3.zero;
        }

        private async UniTaskVoid StartChatBubbleFlowAsync(string messageText, CancellationToken ct)
        {
            try
            {
                await AnimateInAsync(messageText, ct);
                await UniTask.Delay(bubbleIdleTime + AdditionalMessageVisibilityTimeMs(messageText), cancellationToken: ct);
                await AnimateOutAsync(ct);
                animationState = AnimationState.IDLE;
            }
            catch (OperationCanceledException) { animationState = AnimationState.IDLE; }
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

            currentBubbleTailSpriteColor = isMention ? NametagViewConstants.MENTIONED_BUBBLE_TAIL_COLOR : NametagViewConstants.NORMAL_BUBBLE_TAIL_COLOR;
            currentBubbleTailSpriteColor.a = NameTagAlpha;
            bubbleTailSprite.color = currentBubbleTailSpriteColor;

            BackgroundSprite.gameObject.SetActive(!isMention);
            mentionBackgroundSprite.gameObject.SetActive(isMention);

            BackgroundSprite.color = currentSpritesColor;
            mentionBackgroundSprite.color = currentSpritesColor;

            messageContent.SetText(messageText);
            SetHeightAndTextStyle(messageText);
            messageContentRectTransform.ForceUpdateRectTransforms();
            messageContent.ForceMeshUpdate();

            preferredSize = CalculatePreferredSize(out float availableWidthForPrivateMessage);
            messageContentRectTransform.sizeDelta = preferredSize;
            textContentInitialPosition = NametagViewConstants.ZERO_VECTOR;

            preferredSize.x += bubbleMarginOffsetWidth;
            preferredSize.y = messageContent.preferredHeight + bubbleMarginOffsetHeight;

            usernameFinalPosition = CalculateUsernameFinalPosition(preferredSize.x, usernameText.preferredWidth, bubbleMarginOffsetWidth);
            usernameFinalPosition.y = messageContent.preferredHeight + bubbleMarginOffsetHeightThird;

            currentSequence?.Kill();
            currentSequence = DOTween.Sequence();

            currentSequence.AppendInterval(animationInDurationThird);

            if (isClaimedName)
            {
                verifiedIconFinalPosition = CalculateVerifiedIconFinalPosition(usernameFinalPosition.x, cachedUsernameWidth, verifiedIconWidth);
                verifiedIconFinalPosition.y = usernameFinalPosition.y;
                currentSequence.Join(verifiedIcon.DOAnchorPos(verifiedIconFinalPosition, animationInDuration).SetEase(backgroundEaseAnimationCurve));
            }

            if (isPrivateMessage)
            {
                privateMessageIcon.gameObject.SetActive(true);
                privateMessageFinalPosition = CalculatePrivateMessageIconPosition(
                    usernameFinalPosition.x,
                    cachedUsernameWidth,
                    verifiedIconWidth,
                    isClaimedName
                );

                privateMessageFinalPosition.y = usernameFinalPosition.y;
                currentSequence.Join(privateMessageIcon.DOAnchorPos(privateMessageFinalPosition, animationInDuration).SetEase(backgroundEaseAnimationCurve))
                               .Join(privateMessageIconRenderer.DOColor(currentSpritesColor, animationInDurationQuarter));

                if (showPrivateMessageRecipient)
                {
                    privateMessageText.gameObject.SetActive(true);
                    privateMessageText.ForceMeshUpdate();

                    if (privateMessageText.preferredWidth > availableWidthForPrivateMessage) { tempSizeDelta.x = availableWidthForPrivateMessage; }
                    else { tempSizeDelta.x = privateMessageText.preferredWidth; }

                    tempSizeDelta.y = NametagViewConstants.DEFAULT_HEIGHT;
                    privateMessageText.rectTransform.sizeDelta = tempSizeDelta;
                    messageContent.ForceMeshUpdate();

                    privateMessageTextFinalPosition = CalculatePrivateMessageTextPosition(
                        privateMessageFinalPosition.x,
                        privateMessageIconWidth
                    );
                    privateMessageTextFinalPosition.y = usernameFinalPosition.y;
                    currentSequence.Join(privateMessageText.rectTransform.DOAnchorPos(privateMessageTextFinalPosition, animationInDuration).SetEase(backgroundEaseAnimationCurve))
                                   .Join(privateMessageText.DOColor(currentRecipientNameColor, animationInDurationQuarter));
                }
            }

            currentSequence.Join(usernameText.rectTransform.DOAnchorPos(usernameFinalPosition, animationInDuration).SetEase(backgroundEaseAnimationCurve))
                           .Join(this.messageContent.rectTransform.DOAnchorPos(messageContentAnchoredPosition, animationInDuration).SetEase(backgroundEaseAnimationCurve))
                           .Join(this.messageContent.DOColor(currentSpritesColor, animationInDuration));

            if (isMention)
                currentSequence.Join(DOTween.To(() => mentionBackgroundSprite.size, x => mentionBackgroundSprite.size = x, preferredSize, animationInDuration).SetEase(backgroundEaseAnimationCurve));
            else
                currentSequence.Join(DOTween.To(() => BackgroundSprite.size, x => BackgroundSprite.size = x, preferredSize, animationInDuration).SetEase(backgroundEaseAnimationCurve));

            isSpeakingIconFinalPosition = CalculateTalkingIconFinalPosition(usernameFinalPosition.x, cachedUsernameWidth, verifiedIconWidth, isSpeakingIconWidth, privateMessageIconWidth, isSpeaking, isPrivateMessage);
            isSpeakingIconFinalPosition.y = usernameFinalPosition.y;
            currentSequence.Join(isSpeakingIcon.DOAnchorPos(isSpeakingIconFinalPosition, animationInDuration).SetEase(backgroundEaseAnimationCurve));

            await currentSequence.Play().ToUniTask(cancellationToken: ct);
        }

        private void SetHeightAndTextStyle(string message)
        {
            additionalHeight = 0;
            bool isSingleEmoji = NametagViewConstants.SINGLE_EMOJI_REGEX.Match(message).Success;
            if (isSingleEmoji)
            {
                additionalHeight = singleEmojiExtraHeight;
                messageContent.fontSize = singleEmojiSize;
                messageContent.alignment = TextAlignmentOptions.Bottom;
            }
            else
            {
                messageContent.fontSize = NametagViewConstants.MESSAGE_CONTENT_FONT_SIZE;
                messageContent.alignment = TextAlignmentOptions.BottomLeft;
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
                isSpeakingIconWidth,
                isSpeaking,
                isClaimedName
            );

            currentSequence?.Kill();
            currentSequence = DOTween.Sequence();

            if (isClaimedName)
            {
                claimedNameInitialPosition = new Vector2(-(verifiedIconWidth / 2) - (isSpeaking ? isSpeakingIconWidth / 2 : 0), 0);
                currentSequence.Join(usernameText.rectTransform.DOAnchorPos(claimedNameInitialPosition, animationOutDurationHalf).SetEase(NametagViewConstants.LINEAR_EASE));
                currentSequence.Join(verifiedIcon.DOAnchorPos(verifiedIconInitialPosition, animationOutDurationHalf).SetEase(NametagViewConstants.LINEAR_EASE));
            }
            else
                currentSequence.Join(usernameText.rectTransform.DOAnchorPos(NametagViewConstants.ZERO_VECTOR, animationOutDurationHalf).SetEase(NametagViewConstants.LINEAR_EASE));

            if (isPrivateMessage)
            {
                currentSequence.Join(privateMessageIcon.DOAnchorPos(NametagViewConstants.ZERO_VECTOR, animationOutDurationHalf).SetEase(NametagViewConstants.LINEAR_EASE))
                               .Join(privateMessageIconRenderer.DOColor(NametagViewConstants.DEFAULT_TRANSPARENT_COLOR, animationOutDurationTenth));

                if (showPrivateMessageRecipient)
                    currentSequence.Join(privateMessageText.rectTransform.DOAnchorPos(NametagViewConstants.ZERO_VECTOR, animationOutDurationHalf).SetEase(NametagViewConstants.LINEAR_EASE))
                                   .Join(privateMessageText.DOColor(NametagViewConstants.DEFAULT_TRANSPARENT_COLOR, animationOutDurationTenth));
            }

            currentSequence.Join(messageContent.rectTransform.DOAnchorPos(textContentInitialPosition, animationOutDurationHalf).SetEase(NametagViewConstants.LINEAR_EASE))
                           .Join(messageContent.DOColor(NametagViewConstants.DEFAULT_TRANSPARENT_COLOR, animationOutDurationTenth));

            if (isMention)
                currentSequence.Join(DOTween.To(() => mentionBackgroundSprite.size, x => mentionBackgroundSprite.size = x, backgroundFinalSize, animationOutDurationHalf).SetEase(NametagViewConstants.LINEAR_EASE));
            else
                currentSequence.Join(DOTween.To(() => BackgroundSprite.size, x => BackgroundSprite.size = x, backgroundFinalSize, animationOutDurationHalf).SetEase(NametagViewConstants.LINEAR_EASE));

            currentSequence.Join(isSpeakingIcon.DOAnchorPos(isSpeakingIconInitialPosition, animationOutDurationHalf).SetEase(NametagViewConstants.LINEAR_EASE));

            await currentSequence.Play().ToUniTask(cancellationToken: ct);

            privateMessageIcon.gameObject.SetActive(false);
            privateMessageText.gameObject.SetActive(false);
            messageContent.gameObject.SetActive(false);
            BackgroundSprite.gameObject.SetActive(true);
            mentionBackgroundSprite.gameObject.SetActive(false);
        }

        private Vector2 CalculatePreferredSize(out float availableWidthForPrivateMessage) =>
            CalculatePreferredSize(preferredSize, cachedUsernameWidth, nametagMarginOffsetWidth, verifiedIconWidth, isSpeakingIconWidth, isSpeaking, messageContent.preferredWidth, NametagViewConstants.MAX_BUBBLE_WIDTH,
                additionalHeight, messageContent.preferredHeight, isClaimedName, isPrivateMessage,
                privateMessageText.preferredWidth, privateMessageIconWidth, out availableWidthForPrivateMessage);

        [BurstCompile]
        private static float2 CalculatePreferredSize(float2 preferredSize, float usernameWidth, float nametagMarginWidth, float verifiedIconWidth, float isTalkingIconWidth, bool isTalking, float messageWidth, float maxWidth,
            float additionalHeight, float preferredHeight, bool isClaimedName, bool hasPrivateMessageIcon, float privateMessageTextWidth, float privateMessageIconWidth, out float availableWidthForPrivateMessage)
        {
            float baseWidth = usernameWidth + (isClaimedName ? verifiedIconWidth : 0) + (hasPrivateMessageIcon ? privateMessageIconWidth : 0) + (isTalking ? isTalkingIconWidth : 0);

            availableWidthForPrivateMessage = maxWidth - baseWidth;
            float adjustedPrivateMessageWidth = hasPrivateMessageIcon ? Mathf.Min(privateMessageTextWidth, availableWidthForPrivateMessage) : 0;

            float totalWidth = baseWidth + adjustedPrivateMessageWidth;

            float width = Mathf.Min(Mathf.Max(totalWidth, messageWidth), maxWidth);
            float height = preferredHeight + additionalHeight;

            preferredSize.x = width;
            preferredSize.y = height;
            return preferredSize;
        }

        private void SetPrivateMessageText(string recipientValidatedName, string recipientWalletId, Color recipientNameColor)
        {
            string recipientName = BuildRecipientName(recipientValidatedName, recipientWalletId, string.IsNullOrEmpty(recipientWalletId));
            this.currentRecipientNameColor = recipientNameColor;
            privateMessageText.SetText(recipientName);
            recipientNameColor.a = 0;
            privateMessageText.color = recipientNameColor;
        }

        private void UpdateMaterialState(bool transparent)
        {
            if (isTransparent == transparent)
                return;

            isTransparent = transparent;
            sharedMaterial = transparent ? transparentMaterial! : opaqueMaterial!;
        }

        [BurstCompile]
        private static float2 CalculateBackgroundSize(
            float usernameWidth,
            float nametagMarginWidth,
            float verifiedIconWidth,
            float usernameHeight,
            float nametagMarginHeight,
            float isTalkingIconWidth,
            bool isTalking,
            bool isClaimedName)
        {
            float width = usernameWidth + nametagMarginWidth + (isClaimedName ? verifiedIconWidth : 0) + (isTalking ? isTalkingIconWidth : 0);
            float height = usernameHeight + nametagMarginHeight;
            return new float2(width, height);
        }

        [BurstCompile]
        private static float2 CalculateVerifiedIconPosition(
            float usernamePositionX,
            float usernameWidth,
            float verifiedIconWidth,
            float isTalkingIconWidth,
            float nametagMarginHeight,
            bool isTalking) =>
            new float2(
                usernamePositionX + (usernameWidth / 2) + (verifiedIconWidth / 2) - (nametagMarginHeight / 2) - (isTalking ? isTalkingIconWidth / 2 : 0),
                0
            );

        [BurstCompile]
        private static float2 CalculateTalkingIconPosition(
            float usernamePositionX,
            float usernameWidth,
            float verifiedIconWidth,
            bool isClaimedName,
            float nametagMarginHeight) =>
            new float2( usernamePositionX + (usernameWidth / 2) + (isClaimedName ? verifiedIconWidth : 0), 0);

        [BurstCompile]
        private static float2 CalculateUsernamePosition(float usernamePositionX, float verifiedIconWidth, float isTalkingIconWidth, bool isTalking) =>
            new float2(usernamePositionX - (verifiedIconWidth / 2) - (isTalking ? isTalkingIconWidth / 2 : 0), 0);

        [BurstCompile]
        private static float2 CalculateUsernameFinalPosition(float preferredSizeX, float usernameWidth, float bubbleMarginWidth) =>
            new ((-preferredSizeX / 2) + (usernameWidth / 2) + (bubbleMarginWidth / 2), 0);

        [BurstCompile]
        private static float2 CalculateVerifiedIconFinalPosition(float usernameFinalPositionX, float usernameWidth, float verifiedIconWidth) =>
            new (usernameFinalPositionX + (usernameWidth / 2) + (verifiedIconWidth / 2), 0);

        [BurstCompile]
        private static float2 CalculateTalkingIconFinalPosition(
            float usernameFinalPositionX,
            float usernameWidth,
            float verifiedIconWidth,
            float isTalkingIconWidth,
            float privateMessageIconWidth,
            bool isVerifiedName,
            bool isPrivateMessage) =>
            new (usernameFinalPositionX + (usernameWidth / 2) + (isVerifiedName ? verifiedIconWidth : 0), 0);

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

        private string BuildRecipientName(string username, string? walletId, bool hasClaimedName) =>
            string.Concat(NametagViewConstants.RECIPIENT_NAME_START_STRING, hasClaimedName ? username : $"{username}{NametagViewConstants.WALLET_ID_OPENING_STYLE}{walletId}{NametagViewConstants.WALLET_ID_CLOSING_STYLE}");

    }
}
