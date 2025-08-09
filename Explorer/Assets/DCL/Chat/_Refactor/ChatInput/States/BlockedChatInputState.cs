using DCL.Chat.ChatServices;
using UnityEngine.Assertions;

namespace DCL.Chat.ChatInput
{
    /// <summary>
    ///     Due to a reason writing to a user (private conversations) is not allowed.
    ///     Blocked State is valid only if teh chat view is focused
    /// </summary>
    public class BlockedChatInputState : ChatInputState
    {
        private readonly ChatConfig.ChatConfig config;
        private readonly CurrentChannelService currentChannelService;

        public BlockedChatInputState(ChatConfig.ChatConfig config, CurrentChannelService currentChannelService)
        {
            this.config = config;
            this.currentChannelService = currentChannelService;
        }

        public override void Begin()
        {
            context.ChatInputView.Show();

            UpdateBlockedReason();
            context.ChatInputView.maskButton.onClick.AddListener(RequestFocusedState);
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

            context.ChatInputView.SetBlocked(blockedReason);
        }

        public override void End()
        {
            context.ChatInputView.maskButton.onClick.RemoveListener(RequestFocusedState);
        }

        protected override void OnInputBlocked()
        {
            UpdateBlockedReason();
        }

        protected override void OnInputUnblocked()
        {
            ChangeState<TypingEnabledChatInputState>();
        }

        private void RequestFocusedState()
        {
            // It's a global event as we need to switch the state of the whole Chat View
            // Switching the state of the Chat View will lead to switching the state of the Chat Input
            context.ChatEventBus.Publish(new ChatEvents.FocusRequestedEvent());
        }
    }
}
