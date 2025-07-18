using DCL.Chat.EventBus;
using DCL.Chat.Services;
using UnityEngine.Assertions;

namespace DCL.Chat
{
    /// <summary>
    ///     Due to a reason writing to a user (private conversations) is not allowed.
    ///     Blocked State is valid only if teh chat view is focused
    /// </summary>
    public class BlockedChatInputState : ChatInputState
    {
        private readonly ChatConfig config;
        private readonly ICurrentChannelService currentChannelService;

        public BlockedChatInputState(ChatConfig config, ICurrentChannelService currentChannelService)
        {
            this.config = config;
            this.currentChannelService = currentChannelService;
        }

        public override void Begin()
        {
            context.ChatInputView.Show();

            string blockedReason;

            if (currentChannelService.InputState.Success)
            {
                Assert.IsTrue(currentChannelService.InputState.Value != ChatUserStateUpdater.ChatUserState.CONNECTED);

                blockedReason = currentChannelService.InputState.Value switch
                                {
                    ChatUserStateUpdater.ChatUserState.BLOCKED_BY_OWN_USER => config.BlockedByOwnUserMessage,
                    ChatUserStateUpdater.ChatUserState.DISCONNECTED => config.UserOfflineMessage,
                    ChatUserStateUpdater.ChatUserState.PRIVATE_MESSAGES_BLOCKED_BY_OWN_USER => config.OnlyFriendsOwnUserMessage,
                    ChatUserStateUpdater.ChatUserState.PRIVATE_MESSAGES_BLOCKED => config.OnlyFriendsMessage,
                                    _ => string.Empty,
                                };
            }
            else
                blockedReason = currentChannelService.InputState.ErrorMessage!;

            context.ChatInputView.SetBlocked(blockedReason);
            context.ChatInputView.maskButton.onClick.AddListener(RequestFocusedState);
        }

        public override void End()
        {
            context.ChatInputView.maskButton.onClick.RemoveListener(RequestFocusedState);
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
