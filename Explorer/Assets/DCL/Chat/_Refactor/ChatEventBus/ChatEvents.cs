using System.Collections.Generic;
using DCL.Chat.ChatViewModels;
using DCL.Chat.History;

namespace DCL.Chat.EventBus
{
    public class ChatEvents
    {
        #region Initialization events
        
        /// <summary>
        /// Event:          InitialUserStatusLoadedEvent (NOT-USED)
        /// Triggered By:   InitializeChatSystemUseCase
        /// When:           After initial channels are loaded, when the online status for relevant users has been fetched.
        /// Subscribers:    ChatChannelsPresenter (indirectly): Relies on the resulting UserStatusUpdatedEvents to set initial online states.
        /// </summary>
        public struct InitialUserStatusLoadedEvent
        {
            public HashSet<string> Users;
        }
        
        /// <summary>
        /// Event:          InitialChannelsLoadedEvent
        /// Triggered By:   InitializeChatSystemUseCase
        /// When:           The application starts, and the initial list of channels has been loaded from storage.
        /// Subscribers:    ChatChannelsPresenter: Receives the full list and populates the conversation toolbar for the first time.
        /// </summary>
        public struct InitialChannelsLoadedEvent
        {
            public IReadOnlyList<ChatChannel> Channels;
        }
        
        #endregion
        
        #region Channel/Conversation events
        
        /// <summary>
        /// Event:          ChannelUpdatedEvent
        /// Triggered By:   CreateChannelViewModelUseCase
        /// When:           A channel's view-specific data (e.g., a user's profile name/picture) has finished loading asynchronously.
        /// Subscribers:    ChatChannelsPresenter: Finds the channel item in its view and updates its visuals (name, image, etc.).
        /// </summary>
        public struct ChannelUpdatedEvent
        {
            public ChatChannelViewModel ViewModel;
        }
        
        /// <summary>
        /// Event:          ChannelSelectedEvent
        /// Triggered By:   SelectChannelUseCase (on user click) or InitializeChatSystemUseCase (on startup).
        /// When:           A user selects a channel from the list, or the system sets the default channel.
        /// Subscribers:    - ChatChannelsPresenter: Highlights the selected channel in the UI.
        ///                 - ChatMessageFeedPresenter: Clears old messages and loads the history for the new channel.
        ///                 - ChatTitlebarPresenter: Updates the title bar with the new channel's name or profile.
        ///                 - ChatInputPresenter: Checks permissions for the new channel and updates the input field.
        /// </summary>
        public struct ChannelSelectedEvent { public ChatChannel Channel; }
        
        /// <summary>
        /// Event:          ChannelAddedEvent
        /// Triggered By:   (Future) A use case like OpenPrivateConversationUseCase.
        /// When:           A new conversation (e.g., a new DM) is started for the first time.
        /// Subscribers:    ChatChannelsPresenter: Adds a new channel item to the conversation list.
        /// </summary>
        public struct ChannelAddedEvent
        {
            public ChatChannel Channel;
        }

        /// <summary>
        /// Event:          ChannelLeftEvent
        /// Triggered By:   LeaveChannelUseCase
        /// When:           A user chooses to leave a channel (e.g., by closing a DM conversation).
        /// Subscribers:    ChatChannelsPresenter: Removes the corresponding channel item from the conversation list.
        /// </summary>
        public struct ChannelLeftEvent
        {
            public ChatChannel Channel;
        }

        /// <summary>
        /// Event:          UnreadMessagesUpdatedEvent (NOT-USED)
        /// Triggered By:   Systems processing incoming messages.
        /// When:           A new message arrives for a channel that is not currently selected, or read status is updated.
        /// Subscribers:    ChatChannelsPresenter: Updates the unread message count badge on the specific channel item.
        /// </summary>
        public struct UnreadMessagesUpdatedEvent
        {
            public ChatChannel.ChannelId ChannelId;
            public int Count;
        }

        /// <summary>
        /// Event:          UserStatusUpdatedEvent (NOT-USED)
        /// Triggered By:   ChatUserStateUpdater
        /// When:           A user's online status changes (e.g., a friend logs in or out).
        /// Subscribers:    ChatChannelsPresenter: Updates the online status indicator (green/grey dot) on the corresponding DM item.
        /// </summary>
        public struct UserStatusUpdatedEvent
        {
            public string UserId;
            public bool IsOnline;
        }
        
        /// <summary>
        /// Event:          ChannelReadEvent (NOT-USED)
        /// Triggered By:   MarkChannelAsReadUseCase
        /// When:           A user has read all messages in a channel (e.g., by scrolling to the bottom).
        /// Subscribers:    (None currently) This event primarily signals a data model change. UI updates are handled by UnreadMessagesUpdatedEvent.
        /// </summary>
        public struct ChannelReadEvent { public ChatChannel.ChannelId ChannelId; }

        /// <summary>
        ///     Event:          ChatHistoryClearedEvent
        ///     Triggered By:   DeleteChatHistoryCommand
        ///     When:           A user has confirmed they want to delete all messages in the current channel.
        ///     Subscribers:    ChatMessageFeedPresenter: To clear the messages from its view.
        /// </summary>
        public struct ChatHistoryClearedEvent
        {
            public ChatChannel.ChannelId ChannelId;
        }

        #endregion

        #region Memeber List Events

        public struct ChannelMembersUpdatedEvent
        {
            public int MemberCount;
        }

        #endregion
        
        #region General Chat Events
        
        /// <summary>
        /// Event:          FocusRequestedEvent
        /// Triggered By:   ChatInputPresenter
        /// When:           The user clicks on the chat input field, signaling an intent to type.
        /// Subscribers:    ChatFsmController: Transitions the UI to the FocusedChatState.
        /// </summary>
        public struct FocusRequestedEvent { }
        
        /// <summary>
        /// Event:          CloseChatEvent
        /// Triggered By:   ChatTitlebarPresenter
        /// When:           The user clicks the 'X' button in the chat's title bar.
        /// Subscribers:    ChatFsmController: Transitions the UI to the MinimizedChatState.
        /// </summary>
        public struct CloseChatEvent { }
        
        #endregion
        
        #region Chat Message Events
        
        /// <summary>
        /// Event:          MessageSentEvent (NOT-USED)
        /// Triggered By:   (Future) A service that confirms a message was successfully sent to the backend.
        /// When:           After the SendMessageUseCase successfully dispatches a message.
        /// Subscribers:    Could be used for analytics or to update a message's UI from "sending..." to "sent".
        /// </summary>
        public struct MessageSentEvent { public string MessageBody; }
        
        /// <summary>
        /// Event:          MessageReceivedEvent (NOT-USED)
        /// Triggered By:   A low-level chat service listening to the backend
        /// When:           A new message arrives from the server for any channel.
        /// Subscribers:    ChatMessageFeedPresenter: If the message is for the active channel, it appends it to the view.
        /// </summary>
        public struct MessageReceivedEvent {
            public ChatMessage Message;
            public ChatChannel.ChannelId ChannelId;
        }
        
        #endregion

        #region Member List Events

        /// <summary>
        ///     Event:          ChannelMemberUpdatedEvent
        ///     Triggered By:   GetChannelMembersCommand
        ///     When:           An individual member's async data (like a profile thumbnail) has finished loading.
        ///     Subscribers:    ChatMemberListPresenter: Finds the specific member entry in the view and updates its visuals.
        /// </summary>
        public struct ChannelMemberUpdatedEvent
        {
            public ChatMemberListViewModel ViewModel;
        }

        #endregion
        
        #region Miscellaneous Events
        
        /// <summary>
        /// Event:          ToggleMembersEvent
        /// Triggered By:   ChatTitlebarPresenter
        /// When:           The user clicks the button to show or hide the member list for the current channel.
        /// Subscribers:    ChatFsmController: Transitions the UI to/from the MembersChatState.
        /// </summary>
        public struct ToggleMembersEvent { }
        
        /// <summary>
        /// Event:          CurrentChannelStateUpdatedEvent
        /// Triggered By:   ChatUserStateBridge or ChatFsmController states.
        /// When:           A real-time status change affects the current conversation OR the chat UI becomes visible again.
        /// Subscribers:    ChatInputPresenter: Re-runs its permission checks for the current channel and updates the input view.
        /// </summary>
        public struct CurrentChannelStateUpdatedEvent { }
        
        #endregion
    }
}