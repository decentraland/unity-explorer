using System;
using System.Collections.Generic;
using DCL.Chat.ChatStates;
using DCL.Chat.ChatViewModels;
using DCL.Chat.History;
using Utility;

namespace DCL.Chat
{
    public class ChatEventBus : IEventBus
    {
        private readonly IEventBus eventBus = new EventBus(invokeSubscribersOnMainThread: true);

        public void Publish<T>(T evt) => eventBus.Publish(evt);
        public IDisposable Subscribe<T>(Action<T> handler) => eventBus.Subscribe(handler);

#region Chat Message Events
        public void RaiseMessageSentEvent(string messageBody) =>
            Publish(new ChatEvents.MessageSentEvent { MessageBody = messageBody });
#endregion

#region Initialization Events
        public void RaiseInitialChannelsLoadedEvent(IReadOnlyList<ChatChannel> channels) =>
            Publish(new ChatEvents.InitialChannelsLoadedEvent { Channels = channels });
#endregion

#region Channel/Conversation Events
        public void RaiseChannelUpdatedEvent(BaseChannelViewModel viewModel) =>
            Publish(new ChatEvents.ChannelUpdatedEvent { ViewModel = viewModel });

        public void RaiseChannelSelectedEvent(ChatChannel channel, bool fromInitialization = false) =>
            Publish(new ChatEvents.ChannelSelectedEvent { Channel = channel, FromInitialization = fromInitialization });

        public void RaiseChannelAddedEvent(ChatChannel channel) =>
            Publish(new ChatEvents.ChannelAddedEvent { Channel = channel });

        public void RaiseChatResetEvent() =>
            Publish(new ChatEvents.ChatResetEvent());

        public void RaiseChannelLeftEvent(ChatChannel channel) =>
            Publish(new ChatEvents.ChannelLeftEvent { Channel = channel });

        public void RaiseUserStatusUpdatedEvent(ChatChannel.ChannelId channelId, ChatChannel.ChatChannelType channelType, string userId, bool isOnline) =>
            Publish(new ChatEvents.UserStatusUpdatedEvent(channelId, channelType, userId, isOnline));

        public void RaiseChannelUsersStatusUpdated(ChatChannel.ChannelId channelId, ChatChannel.ChatChannelType channelType, IReadOnlyCollection<string> onlineUsers) =>
            Publish(new ChatEvents.ChannelUsersStatusUpdated(channelId, channelType, onlineUsers));

        public void RaiseChatHistoryClearedEvent(ChatChannel.ChannelId channelId) =>
            Publish(new ChatEvents.ChatHistoryClearedEvent { ChannelId = channelId });
#endregion

#region General Chat Events
        public void RaiseFocusRequestedEvent() =>
            Publish(new ChatEvents.FocusRequestedEvent());

        public void RaiseCloseChatEvent() =>
            Publish(new ChatEvents.CloseChatEvent());

        public void RaiseChatStateChangedEvent(ChatState currentState) =>
            Publish(new ChatEvents.ChatStateChangedEvent { CurrentState = currentState });

        public void RaiseToggleChatEvent() =>
            Publish(new ChatEvents.ToggleChatEvent());
#endregion

#region Miscellaneous Events
        public void RaiseToggleMembersEvent() =>
            Publish(new ChatEvents.ToggleMembersEvent());

        public void RaiseCurrentChannelStateUpdatedEvent() =>
            Publish(new ChatEvents.CurrentChannelStateUpdatedEvent());

        public void RaiseClickableBlockedInputClickedEvent() =>
            Publish(new ChatEvents.ClickableBlockedInputClickedEvent());
#endregion

#region External Chat Request Events
        public void RaiseInsertTextInChatRequestedEvent(string text) =>
            Publish(new ChatEvents.InsertTextInChatRequestedEvent { Text = text });

        public void RaiseClearAndInsertTextInChatRequestedEvent(string text) =>
            Publish(new ChatEvents.ClearAndInsertTextInChatRequestedEvent { Text = text });

        public void RaiseOpenPrivateConversationRequestedEvent(string userId) =>
            Publish(new ChatEvents.OpenPrivateConversationRequestedEvent { UserId = userId });

        public void RaiseOpenCommunityConversationRequestedEvent(string communityId) =>
            Publish(new ChatEvents.OpenCommunityConversationRequestedEvent { CommunityId = communityId });

        public void RaiseStartCallEvent() =>
            Publish(new ChatEvents.StartCallEvent());

        public void InsertText(string text) =>
            RaiseInsertTextInChatRequestedEvent(text);

        public void ClearAndInsertText(string text) =>
            RaiseClearAndInsertTextInChatRequestedEvent(text);

        public void OpenPrivateConversationUsingUserId(string userId) =>
            RaiseOpenPrivateConversationRequestedEvent(userId);

        public void OpenCommunityConversationUsingCommunityId(string communityId) =>
            RaiseOpenCommunityConversationRequestedEvent(communityId);

        public void StartCallInCurrentConversation() =>
            RaiseStartCallEvent();
#endregion
    }
}
