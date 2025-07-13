using System.Collections.Generic;
using DCL.Chat.ChatViewModels;
using DCL.Chat.History;

namespace DCL.Chat.EventBus
{
    public class ChatEvents
    {
        #region Initialization events
        
        public struct InitialUserStatusLoadedEvent
        {
            public HashSet<string> Users;
        }
        
        // Published by the InitializeChatSystemUseCase when the initial set of channels is loaded.
        // The ChannelListPresenter listens to this to populate its initial view.
        public struct InitialChannelsLoadedEvent
        {
            public IReadOnlyList<ChatChannel> Channels;
        }
        
        #endregion
        
        #region Channel/Conversation events
        
        // NOTE: Chat channel events
        public struct ChannelUpdatedEvent
        {
            public ChatChannelViewModel ViewModel;
        }
        
        public struct ChannelSelectedEvent { public ChatChannel Channel; }
        
        // Published when a new channel is created (e.g., a new DM is started).
        public struct ChannelAddedEvent
        {
            public ChatChannel Channel;
        }

        // Published by the LeaveChannelUseCase after a channel has been successfully removed.
        public struct ChannelLeftEvent
        {
            public ChatChannel.ChannelId ChannelId;
        }

        // Published by the MarkChannelAsReadUseCase or when a new message arrives for an unread channel.
        public struct UnreadMessagesUpdatedEvent
        {
            public ChatChannel.ChannelId ChannelId;
            public int Count;
        }

        // Published when a user's online status changes.
        public struct UserStatusUpdatedEvent
        {
            public string UserId;
            public bool IsOnline;
        }
        public struct ChannelReadEvent { public ChatChannel.ChannelId ChannelId; }
        
        #endregion
        
        #region General Chat Events
        public struct FocusRequestedEvent { }
        public struct CloseChatEvent { }
        
        #endregion
        
        #region Chat Message Events
        
        public struct MessageSentEvent { public string MessageBody; }
        public struct MessageReceivedEvent { 
            public ChatMessage Message;
            public ChatChannel.ChannelId ChannelId;
        }
        
        #endregion
 
        #region Miscellaneous Events
        
        public struct ClickOutsideEvent { }
        public struct ToggleMembersEvent { public bool IsVisible; }
        #endregion
    }
}