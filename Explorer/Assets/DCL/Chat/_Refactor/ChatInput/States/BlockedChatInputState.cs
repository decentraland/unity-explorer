using DCL.Chat.ChatServices;
using MVC;
using UnityEngine.Assertions;
using Utility;

namespace DCL.Chat.ChatInput
{
    /// <summary>
    ///     Due to a reason writing to a user (private conversations) is not allowed.
    ///     Blocked State is valid only if teh chat view is focused
    /// </summary>
    public class BlockedChatInputState : ChatInputState
    {
        private readonly MVCStateMachine<ChatInputState> stateMachine;
        private readonly ChatInputView view;
        private readonly IEventBus eventBus;
        private readonly ChatConfig.ChatConfig config;
        private readonly CurrentChannelService currentChannelService;

        public BlockedChatInputState(MVCStateMachine<ChatInputState> stateMachine, ChatInputView view, IEventBus eventBus, ChatConfig.ChatConfig config, CurrentChannelService currentChannelService)
        {
            this.stateMachine = stateMachine;
            this.view = view;
            this.eventBus = eventBus;
            this.config = config;
            this.currentChannelService = currentChannelService;
        }

        public override void Enter()
        {
            view.Show();

            UpdateBlockedReason();
            view.maskButton.onClick.AddListener(RequestFocusedState);
        }

        private void UpdateBlockedReason()
        {
            string blockedReason;

            if (currentChannelService.InputState.Success)
            {
                Assert.IsTrue(currentChannelService.InputState.Value != PrivateConversationUserStateService.ChatUserState.CONNECTED);

                blockedReason = currentChannelService.InputState.Value switch
                {
                    PrivateConversationUserStateService.ChatUserState.BLOCKED_BY_OWN_USER => config.BlockedByOwnUserMessage,
                    PrivateConversationUserStateService.ChatUserState.DISCONNECTED => config.UserOfflineMessage,
                    PrivateConversationUserStateService.ChatUserState.PRIVATE_MESSAGES_BLOCKED_BY_OWN_USER => config.OnlyFriendsOwnUserMessage,
                    PrivateConversationUserStateService.ChatUserState.PRIVATE_MESSAGES_BLOCKED => config.OnlyFriendsMessage,
                    _ => string.Empty
                };
            }
            else
                blockedReason = currentChannelService.InputState.ErrorMessage!;

            view.maskButton.onClick.RemoveListener(BlockedInputClicked);
            if (currentChannelService.InputState is { Success: true, Value: PrivateConversationUserStateService.ChatUserState.PRIVATE_MESSAGES_BLOCKED_BY_OWN_USER })
                view.maskButton.onClick.AddListener(BlockedInputClicked);

            view.SetBlocked(blockedReason);
        }

        public override void Exit()
        {
            view.maskButton.onClick.RemoveListener(RequestFocusedState);
            view.maskButton.onClick.RemoveListener(BlockedInputClicked);
        }

        protected override void OnInputBlocked()
        {
            UpdateBlockedReason();
        }

        protected override void OnInputUnblocked()
        {
            stateMachine.Enter<TypingEnabledChatInputState>();
        }

        private void BlockedInputClicked() =>
            eventBus.Publish(new ChatEvents.ClickableBlockedInputClickedEvent());

        private void RequestFocusedState()
        {
            // It's a global event as we need to switch the state of the whole Chat View
            // Switching the state of the Chat View will lead to switching the state of the Chat Input
            eventBus.Publish(new ChatEvents.FocusRequestedEvent());
        }
    }
}
