using DCL.Chat.ChatFriends;
using DCL.Chat.ChatInput;
using DCL.Chat.ChatMessages;
using DCL.VoiceChat;
using DG.Tweening;

namespace DCL.Chat
{
    public class ChatUIMediator
    {
        private readonly ChatPanelView panelView;
        private readonly ChatConfig.ChatConfig config;
        private readonly CommunityVoiceChatSubTitleButtonPresenter subTitleButtonPresenter;
        private readonly IVoiceChatOrchestrator voiceChatOrchestrator;
        private readonly ChatTitlebarPresenter titleBarPresenter;
        private readonly ChatChannelsPresenter channelListPresenter;
        private readonly ChatMessageFeedPresenter messageFeedPresenter;
        internal readonly ChatInputPresenter chatInputPresenter;
        private readonly ChatMemberListPresenter memberListPresenter;

        public ChatUIMediator(
            ChatPanelView panelView,
            ChatConfig.ChatConfig config,
            ChatTitlebarPresenter titleBarPresenter,
            ChatChannelsPresenter channelListPresenter,
            ChatMessageFeedPresenter messageFeedPresenter,
            ChatInputPresenter chatInputPresenter,
            ChatMemberListPresenter memberListPresenter,
            CommunityVoiceChatSubTitleButtonPresenter subTitleButtonPresenter,
            IVoiceChatOrchestrator voiceChatOrchestrator
            )
        {
            this.panelView = panelView;
            this.config = config;
            this.titleBarPresenter = titleBarPresenter;
            this.channelListPresenter = channelListPresenter;
            this.messageFeedPresenter = messageFeedPresenter;
            this.chatInputPresenter = chatInputPresenter;
            this.memberListPresenter = memberListPresenter;
            this.subTitleButtonPresenter = subTitleButtonPresenter;
            this.voiceChatOrchestrator = voiceChatOrchestrator;
        }

        public void SetupForDefaultState(bool animate)
        {
            titleBarPresenter.Show();
            titleBarPresenter.ShowMembersView(isMemberListVisible:false);

            channelListPresenter.Show();
            messageFeedPresenter.TryActivate();
            chatInputPresenter.ShowUnfocused();
            memberListPresenter.Hide();

            SetPanelsFocus(isFocused: false, animate);

            subTitleButtonPresenter.Show();
        }

        public void SetupForFocusedState()
        {
            titleBarPresenter.Show();
            titleBarPresenter.ShowMembersView(isMemberListVisible:false);

            channelListPresenter.Show();
            messageFeedPresenter.TryActivate();
            chatInputPresenter.ShowFocusedAsync().Forget();
            memberListPresenter.Hide();

            SetPanelsFocus(isFocused: true, animate: false);

            subTitleButtonPresenter.Show();
        }

        public void SetupForMembersState()
        {
            titleBarPresenter.Show();
            titleBarPresenter.ShowMembersView(isMemberListVisible:true);

            channelListPresenter.Hide();
            messageFeedPresenter.TryDeactivate();
            chatInputPresenter.Hide();
            memberListPresenter.Show();

            SetPanelsFocus(isFocused: false, animate: false);

            subTitleButtonPresenter.Hide();
        }

        public void SetupForMinimizedState()
        {
            titleBarPresenter.Hide();
            titleBarPresenter.ShowMembersView(isMemberListVisible:false);

            channelListPresenter.Hide();
            messageFeedPresenter.TryDeactivate();
            memberListPresenter.Hide();
            chatInputPresenter.ShowUnfocused();

            SetPanelsFocus(isFocused: false, animate: true);

            subTitleButtonPresenter.Hide();
        }

        public void SetupForHiddenState()
        {
            titleBarPresenter.Hide();
            titleBarPresenter.ShowMembersView(isMemberListVisible:false);

            channelListPresenter.Hide();
            messageFeedPresenter.TryDeactivate();
            chatInputPresenter.Hide();
            memberListPresenter.Hide();

            SetPanelsFocus(isFocused: false, animate: false);

            voiceChatOrchestrator.ChangePanelState(VoiceChatPanelState.HIDDEN, true);
            subTitleButtonPresenter.Hide();
        }

        internal void SetPanelsFocus(bool isFocused, bool animate)
        {
            float duration = animate ? config.PanelsFadeDuration : 0f;
            Ease ease = config.PanelsFadeEase;

            panelView.SetSharedBackgroundFocusState(isFocused, animate, duration, ease);
            messageFeedPresenter.SetFocusState(isFocused, animate, duration, ease);
            channelListPresenter.SetFocusState(isFocused, animate, duration, ease);
            titleBarPresenter.SetFocusState(isFocused, animate, duration, ease);
            subTitleButtonPresenter.SetFocusState(isFocused, animate, duration);
        }
    }
}
