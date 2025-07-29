using DCL.Chat.ChatFriends;
using DCL.Chat.ChatMessages;

namespace DCL.Chat.ChatMediator
{
    using DG.Tweening;

    public class ChatUIMediator
    {
        private readonly ChatMainView mainView;
        private readonly ChatConfig config;
        internal readonly ChatTitlebarPresenter titleBarPresenter;
        internal readonly ChatChannelsPresenter channelListPresenter;
        internal readonly ChatMessageFeedPresenter messageFeedPresenter;
        internal readonly ChatInputPresenter chatInputPresenter;
        internal readonly ChatMemberListPresenter memberListPresenter;

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
            titleBarPresenter.ShowMembersView(isMemberListVisible:false);

            channelListPresenter.Show();
            messageFeedPresenter.TryActivate();
            chatInputPresenter.ShowUnfocused();
            memberListPresenter.Hide();

            SetPanelsFocus(isFocused: false, animate);
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
