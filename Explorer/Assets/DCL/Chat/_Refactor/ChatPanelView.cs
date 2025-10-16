using DCL.Chat.ChatInput;
using DCL.Chat.ChatMessages;
using DCL.Chat.ChatViews;
using DCL.VoiceChat;
using DG.Tweening;
using UnityEngine;

namespace DCL.Chat
{
    public class ChatPanelView : MonoBehaviour
    {
        [SerializeField] private CanvasGroup sharedBackgroundCanvasGroup = null!;
        [field: SerializeField] public ChatChannelsView ConversationToolbarView2 { get; private set; } = null!;
        [field: SerializeField] public ChatMessageFeedView MessageFeedView { get; private set; } = null!;
        [field: SerializeField] public ChatInputView InputView { get; private set; } = null!;
        [field: SerializeField] public ChatTitlebarView TitlebarView { get; private set; } = null!;
        [field: SerializeField] public ChannelMemberFeedView MemberListView { get; private set; } = null!;

        [field: Header("Voice Chat")]
        [field: SerializeField] public JoinCommunityLiveStreamChatSubTitleButtonView JoinCommunityLiveStreamSubTitleButton { get; private set; } = null!;

        public void SetSharedBackgroundFocusState(bool isFocused, bool animate, float duration, Ease easing)
        {
            // This is the logic that was previously in ChatMessageFeedView, now in its correct home.
            sharedBackgroundCanvasGroup.DOKill();

            float targetAlpha = isFocused ? 1.0f : 0.0f;
            float fadeDuration = animate ? duration : 0f;

            if (isFocused && !sharedBackgroundCanvasGroup.gameObject.activeSelf)
                sharedBackgroundCanvasGroup.gameObject.SetActive(true);

            sharedBackgroundCanvasGroup.DOFade(targetAlpha, fadeDuration)
                                       .SetEase(easing)
                                       .OnComplete(() =>
                                        {
                                            if (!isFocused)
                                            {
                                                sharedBackgroundCanvasGroup.gameObject.SetActive(false);
                                            }
                                        });
        }
    }
}
