namespace DCL.Chat.ChatMediator
{
    using DG.Tweening;

    public class ChatUIMediator
    {
        private readonly ChatMainView mainView;
        private readonly ChatConfig config;
        private readonly ChatTitlebarPresenter titleBarPresenter;
        private readonly ChatChannelsPresenter channelListPresenter;
        private readonly ChatMessageFeedPresenter messageFeedPresenter;
        private readonly ChatInputPresenter chatInputPresenter;
        private readonly ChatMemberListPresenter memberListPresenter;

        public ChatUIMediator(
            ChatMainView mainView,
            ChatConfig config,
            ChatTitlebarPresenter titleBarPresenter,
            ChatChannelsPresenter channelListPresenter,
            ChatMessageFeedPresenter messageFeedPresenter,
            ChatInputPresenter chatInputPresenter,
            ChatMemberListPresenter memberListPresenter)
        {
            this.mainView = mainView;
            this.config = config;
            this.titleBarPresenter = titleBarPresenter;
            this.channelListPresenter = channelListPresenter;
            this.messageFeedPresenter = messageFeedPresenter;
            this.chatInputPresenter = chatInputPresenter;
            this.memberListPresenter = memberListPresenter;
        }
        
        public void SetupForDefaultState(bool animate)
        {
            titleBarPresenter.Show();
            channelListPresenter.Show();
            messageFeedPresenter.Show();
            chatInputPresenter.Show();
            chatInputPresenter.SetInactiveMode();
            memberListPresenter.Hide();
            
            SetPanelsFocus(isFocused: false, animate);
        }

        public void SetupForFocusedState()
        {
            titleBarPresenter.Show();
            channelListPresenter.Show();
            messageFeedPresenter.Show();
            chatInputPresenter.Show();
            chatInputPresenter.SetActiveMode();
            memberListPresenter.Hide();
            
            SetPanelsFocus(isFocused: true, animate: false);
        }

        public void SetupForMembersState()
        {
            titleBarPresenter.ShowMembersView();
            channelListPresenter.Hide();
            messageFeedPresenter.Hide();
            chatInputPresenter.Hide();
            memberListPresenter.Show();

            SetPanelsFocus(isFocused: true, animate: true);
        }

        public void SetupForMinimizedState()
        {
            titleBarPresenter.Hide();
            channelListPresenter.Hide();
            messageFeedPresenter.Hide();
            memberListPresenter.Hide();
            
            chatInputPresenter.Show();
            chatInputPresenter.SetInactiveMode();
            
            SetPanelsFocus(isFocused: false, animate: true);
        }

        internal void SetPanelsFocus(bool isFocused, bool animate)
        {
            float duration = animate ? config.PanelsFadeDuration : 0f;
            Ease ease = config.PanelsFadeEase;

            mainView.SetSharedBackgroundFocusState(isFocused, animate, duration, ease);
            messageFeedPresenter.SetFocusState(isFocused, animate, duration, ease);
            channelListPresenter.SetFocusState(isFocused, animate, duration, ease);
            titleBarPresenter.SetFocusState(isFocused, animate, duration, ease);
        }
    }
}