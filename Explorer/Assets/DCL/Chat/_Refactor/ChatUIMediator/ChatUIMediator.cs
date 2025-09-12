using DCL.Chat.ChatFriends;
using DCL.Chat.ChatInput;
using DCL.Chat.ChatMessages;
using DCL.VoiceChat;
using DG.Tweening;

namespace DCL.Chat
{
    public class ChatUIMediator
    {
        private readonly ChatMainView mainView;
        private readonly ChatConfig.ChatConfig config;
        private readonly CommunityVoiceChatSubTitleButtonPresenter subTitleButtonPresenter;
        private readonly IVoiceChatOrchestrator voiceChatOrchestrator;
        internal readonly ChatTitlebarPresenter titleBarPresenter;
        internal readonly ChatChannelsPresenter channelListPresenter;
        internal readonly ChatMessageFeedPresenter messageFeedPresenter;
        internal readonly ChatInputPresenter chatInputPresenter;
        internal readonly ChatMemberListPresenter memberListPresenter;

        public ChatUIMediator(
            ChatMainView mainView,
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
            this.mainView = mainView;
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
            subTitleButtonPresenter.OnMemberListVisibilityChanged(false);

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
            memberListPresenter.Show();

            SetPanelsFocus(isFocused: false, animate: false);
        }

        public void SetupForMinimizedState()
        {
            titleBarPresenter.Hide();
            titleBarPresenter.ShowMembersView(isMemberListVisible:false);

            subTitleButtonPresenter.Hide();

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

            subTitleButtonPresenter.Hide();

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

            if (voiceChatOrchestrator.CurrentVoiceChatType.Value != VoiceChatType.COMMUNITY) return;

            //When the chat changes focus and the Voice Chat panel is expanded we need to change the size of it
            switch (isFocused)
            {
                case false when
                    voiceChatOrchestrator.CurrentVoiceChatPanelSize.Value == VoiceChatPanelSize.EXPANDED:
                    voiceChatOrchestrator.ChangePanelSize(VoiceChatPanelSize.EXPANDED_WITHOUT_BUTTONS); break;
                case true when voiceChatOrchestrator.CurrentVoiceChatPanelSize.Value == VoiceChatPanelSize.EXPANDED_WITHOUT_BUTTONS:
                    voiceChatOrchestrator.ChangePanelSize(VoiceChatPanelSize.EXPANDED); break;
            }
        }
    }
}
