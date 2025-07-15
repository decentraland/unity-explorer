using System;
using DCL.Chat.EventBus;
using Utilities;

namespace DCL.Chat.Services
{
    namespace DCL.Chat
    {
        // This class listens to low-level user state events and translates them
        // into high-level events that the UI presenters can understand.
        public class ChatUserStateBridge : IDisposable
        {
            private readonly IChatUserStateEventBus userStateEventBus;
            private readonly IEventBus eventBus;
            private readonly ICurrentChannelService currentChannelService;

            public ChatUserStateBridge(
                IChatUserStateEventBus userStateEventBus,
                IEventBus eventBus,
                ICurrentChannelService currentChannelService)
            {
                this.userStateEventBus = userStateEventBus;
                this.eventBus = eventBus;
                this.currentChannelService = currentChannelService;
                
                this.userStateEventBus.UserConnectionStateChanged += HandleUserConnectionStateChanged;
                this.userStateEventBus.UserBlocked += HandleUserBlocked;
                this.userStateEventBus.UserDisconnected += HandleUserDisconnected;
                this.userStateEventBus.CurrentConversationUserAvailable += HandleCurrentConversationUserAvailable;
                this.userStateEventBus.CurrentConversationUserUnavailable += HandleCurrentConversationUserUnavailable;
            }

            public void Dispose()
            {
                userStateEventBus.UserConnectionStateChanged -= HandleUserConnectionStateChanged;
                userStateEventBus.UserBlocked -= HandleUserBlocked;
                userStateEventBus.UserDisconnected -= HandleUserDisconnected;
                userStateEventBus.CurrentConversationUserAvailable -= HandleCurrentConversationUserAvailable;
                userStateEventBus.CurrentConversationUserUnavailable -= HandleCurrentConversationUserUnavailable;
            }

            private void HandleUserConnectionStateChanged(string userId, bool isOnline)
            {
                // This is a direct translation.
                // The ChatChannelsPresenter is already listening for this.
                eventBus.Publish(new ChatEvents.UserStatusUpdatedEvent
                {
                    UserId = userId, IsOnline = isOnline
                });
            }

            private void HandleUserBlocked(string userId)
            {
                // If the blocked user is the one we are currently chatting with,
                // we need to force the input box to update its state.
                if (currentChannelService.CurrentChannelId.Id == userId)
                {
                    eventBus.Publish(new ChatEvents.CurrentChannelStateUpdatedEvent());
                }
            }

            private void HandleUserDisconnected(string userId)
            {
                if (currentChannelService.CurrentChannelId.Id == userId)
                {
                    eventBus.Publish(new ChatEvents.CurrentChannelStateUpdatedEvent());
                }
            }

            // These two events specifically target
            // the input box state for the active conversation.
            private void HandleCurrentConversationUserAvailable() =>
                eventBus.Publish(new ChatEvents.CurrentChannelStateUpdatedEvent());

            private void HandleCurrentConversationUserUnavailable() =>
                eventBus.Publish(new ChatEvents.CurrentChannelStateUpdatedEvent());
        }
    }
}