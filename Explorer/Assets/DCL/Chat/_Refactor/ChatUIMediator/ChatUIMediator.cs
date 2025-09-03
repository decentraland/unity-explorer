﻿using DCL.Chat.ChatFriends;
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
        private readonly CommunityVoiceChatSubTitleButtonController subTitleButtonController;
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
            CommunityVoiceChatSubTitleButtonController subTitleButtonController)
        {
            this.mainView = mainView;
            this.config = config;
            this.titleBarPresenter = titleBarPresenter;
            this.channelListPresenter = channelListPresenter;
            this.messageFeedPresenter = messageFeedPresenter;
            this.chatInputPresenter = chatInputPresenter;
            this.memberListPresenter = memberListPresenter;
            this.subTitleButtonController = subTitleButtonController;
        }

        public void SetupForDefaultState(bool animate)
        {
            titleBarPresenter.Show();
            titleBarPresenter.ShowMembersView(isMemberListVisible:false);

            channelListPresenter.Show();
            messageFeedPresenter.TryActivate();
            chatInputPresenter.ShowUnfocused();
            memberListPresenter.Hide();
            subTitleButtonController.OnMemberListVisibilityChanged(false);

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
            subTitleButtonController.OnMemberListVisibilityChanged(false);

            SetPanelsFocus(isFocused: true, animate: false);
        }

        public void SetupForMembersState()
        {
            titleBarPresenter.Show();
            titleBarPresenter.ShowMembersView(isMemberListVisible:true);

            subTitleButtonController.OnMemberListVisibilityChanged(true);
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
            subTitleButtonController.OnMemberListVisibilityChanged(false);

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
