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
        internal readonly ChatTitlebarPresenter titleBarPresenter;
        internal readonly ChatChannelsPresenter channelListPresenter;
        internal readonly ChatMessageFeedPresenter messageFeedPresenter;
        internal readonly ChatInputPresenter chatInputPresenter;
        internal readonly ChatMemberFeedPresenter memberFeedPresenter;

        public ChatUIMediator(
            ChatPanelView panelView,
            ChatConfig.ChatConfig config,
            ChatTitlebarPresenter titleBarPresenter,
            ChatChannelsPresenter channelListPresenter,
            ChatMessageFeedPresenter messageFeedPresenter,
            ChatInputPresenter chatInputPresenter,
            ChatMemberFeedPresenter memberFeedPresenter,
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
            this.memberFeedPresenter = memberFeedPresenter;
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
            memberFeedPresenter.Hide();

            subTitleButtonPresenter.OnMemberListVisibilityChanged(false);
            subTitleButtonPresenter.Hide();

            SetPanelsFocus(isFocused: false, animate);
        }

        public void SetupForFocusedState()
        {
            titleBarPresenter.Show();
            titleBarPresenter.ShowMembersView(isMemberListVisible:false);

            channelListPresenter.Show();
            messageFeedPresenter.TryActivate();
            chatInputPresenter.ShowFocusedAsync().Forget();
            memberFeedPresenter.Hide();

            subTitleButtonPresenter.Show();
            subTitleButtonPresenter.OnMemberListVisibilityChanged(false);

            SetPanelsFocus(isFocused: true, animate: false);
        }

        public void SetupForMembersState()
        {
            titleBarPresenter.Show();
            titleBarPresenter.ShowMembersView(isMemberListVisible:true);

            subTitleButtonPresenter.OnMemberListVisibilityChanged(true);
            channelListPresenter.Hide();
            messageFeedPresenter.TryDeactivate();
            chatInputPresenter.Hide();
            memberFeedPresenter.Show();

            voiceChatOrchestrator.ChangePanelState(VoiceChatPanelState.FOCUSED, true);

            SetPanelsFocus(isFocused: false, animate: false);
        }

        public void SetupForMinimizedState()
        {
            titleBarPresenter.Hide();
            titleBarPresenter.ShowMembersView(isMemberListVisible:false);
            subTitleButtonPresenter.OnMemberListVisibilityChanged(false);

            subTitleButtonPresenter.Hide();
            voiceChatOrchestrator.ChangePanelState(VoiceChatPanelState.UNFOCUSED, true);

            channelListPresenter.Hide();
            messageFeedPresenter.TryDeactivate();
            memberFeedPresenter.Hide();
            chatInputPresenter.ShowUnfocused();

            SetPanelsFocus(isFocused: false, animate: true);
        }

        public void SetupForHiddenState()
        {
            titleBarPresenter.Hide();
            titleBarPresenter.ShowMembersView(isMemberListVisible:false);

            subTitleButtonPresenter.Hide();

            channelListPresenter.Hide();
            messageFeedPresenter.TryDeactivate();
            chatInputPresenter.Hide();
            memberFeedPresenter.Hide();

            voiceChatOrchestrator.ChangePanelState(VoiceChatPanelState.HIDDEN, true);

            SetPanelsFocus(isFocused: false, animate: false);
        }

        internal void SetPanelsFocus(bool isFocused, bool animate)
        {
            float duration = animate ? config.PanelsFadeDuration : 0f;
            Ease ease = config.PanelsFadeEase;

            panelView.SetSharedBackgroundFocusState(isFocused, animate, duration, ease);
            messageFeedPresenter.SetFocusState(isFocused, animate, duration, ease);
            channelListPresenter.SetFocusState(isFocused, animate, duration, ease);
            titleBarPresenter.SetFocusState(isFocused, animate, duration, ease);
        }
    }
}
