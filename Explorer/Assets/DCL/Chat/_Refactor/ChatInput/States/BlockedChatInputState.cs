using DCL.Chat.EventBus;
using DCL.Chat.Services;
using UnityEngine.Assertions;

namespace DCL.Chat
{
    /// <summary>
    ///     Due to a reason writing to a user (private conversations) is not allowed.
    ///     Blocked state doesn't change if it is unfocused
    /// </summary>
    public class BlockedChatInputState : ChatInputState
    {
        private readonly ICurrentChannelService currentChannelService;

        public BlockedChatInputState(ICurrentChannelService currentChannelService)
        {
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
                                    ChatUserStateUpdater.ChatUserState.BLOCKED_BY_OWN_USER => "To message this user you must first unblock them.",
                                    ChatUserStateUpdater.ChatUserState.DISCONNECTED => "The user you are trying to message is offline.",
                                    ChatUserStateUpdater.ChatUserState.PRIVATE_MESSAGES_BLOCKED_BY_OWN_USER => "Add this user as a friend to chat, or update your <b><u>DM settings</b></u> to connect with everyone.",
                                    ChatUserStateUpdater.ChatUserState.PRIVATE_MESSAGES_BLOCKED => "To message this user you must first unblock them.",
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
            // TODO I need to know the parent state in order to resolve the transition: to Unfocused state or to TypingEnabled state
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
