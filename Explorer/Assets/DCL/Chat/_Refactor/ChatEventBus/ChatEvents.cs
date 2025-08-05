using DCL.Chat.ChatViewModels;
using DCL.Chat.History;
using DCL.Friends.UserBlocking;
using System.Collections.Generic;

namespace DCL.Chat
{
    public class ChatEvents
    {
#region Chat Message Events
        /// <summary>
        ///     Event:          MessageSentEvent (NOT-USED)
        ///     Triggered By:   (Future) A service that confirms a message was successfully sent to the backend.
        ///     When:           After the SendMessageCommand successfully dispatches a message.
        ///     Subscribers:    Could be used for analytics or to update a message's UI from "sending..." to "sent".
        /// </summary>
        public struct MessageSentEvent
        {
            public string MessageBody;
        }

        // /// <summary>
        // /// Event:          MessageReceivedEvent (NOT-USED)
        // /// Triggered By:   A low-level chat service listening to the backend
        // /// When:           A new message arrives from the server for any channel.
        // /// Subscribers:    ChatMessageFeedPresenter: If the message is for the active channel, it appends it to the view.
        // /// </summary>
        // public struct MessageReceivedEvent
        // {
        //     public ChatMessage Message;
        //     public ChatChannel.ChannelId ChannelId;
        // }
#endregion

#region Initialization events

        /// <summary>
        ///     Event:          InitialChannelsLoadedEvent
        ///     Triggered By:   InitializeChatSystemCommand
        ///     When:           The application starts, and the initial list of channels has been loaded from storage.
        ///     Subscribers:    ChatChannelsPresenter: Receives the full list and populates the conversation toolbar for the first time.
        /// </summary>
        public struct InitialChannelsLoadedEvent
        {
            public IReadOnlyList<ChatChannel> Channels;
        }
#endregion

#region Channel/Conversation events
        /// <summary>
        ///     Event:          ChannelUpdatedEvent
        ///     Triggered By:   CreateChannelViewModelCommand
        ///     When:           A channel's view-specific data (e.g., a user's profile name/picture) has finished loading asynchronously.
        ///     Subscribers:    ChatChannelsPresenter: Finds the channel item in its view and updates its visuals (name, image, etc.).
        /// </summary>
        public struct ChannelUpdatedEvent
        {
            public BaseChannelViewModel ViewModel;
        }

        /// <summary>
        ///     Event:          ChannelSelectedEvent
        ///     Triggered By:   SelectChannelCommand (on user click) or InitializeChatSystemCommand (on startup).
        ///     When:           A user selects a channel from the list, or the system sets the default channel.
        ///     Subscribers:    - ChatChannelsPresenter: Highlights the selected channel in the UI.
        ///     - ChatMessageFeedPresenter: Clears old messages and loads the history for the new channel.
        ///     - ChatTitlebarPresenter: Updates the title bar with the new channel's name or profile.
        ///     - ChatInputPresenter: Checks permissions for the new channel and updates the input field.
        /// </summary>
        public struct ChannelSelectedEvent
        {
            public ChatChannel Channel;
        }

        /// <summary>
        ///     Event:          ChannelAddedEvent
        ///     Triggered By:   (Future) A use case like OpenPrivateConversationCommand.
        ///     When:           A new conversation (e.g., a new DM) is started for the first time.
        ///     Subscribers:    ChatChannelsPresenter: Adds a new channel item to the conversation list.
        /// </summary>
        public struct ChannelAddedEvent
        {
            public ChatChannel Channel;
        }

        /// <summary>
        ///     Event:          ChannelLeftEvent
        ///     Triggered By:   LeaveChannelCommand
        ///     When:           A user chooses to leave a channel (e.g., by closing a DM conversation).
        ///     Subscribers:    ChatChannelsPresenter: Removes the corresponding channel item from the conversation list.
        /// </summary>
        public struct ChannelLeftEvent
        {
            public ChatChannel Channel;
        }

        /// <summary>
        ///     Event:          UserStatusUpdatedEvent
        ///     Triggered By:   ChatUserStateUpdater
        ///     When:           A user's online status changes for the given channel (e.g., a friend logs in or out). User State can be different in different channels.
        ///     Subscribers:    ChatChannelsPresenter: Updates the online status indicator (green/grey dot) on the corresponding DM item.
        /// </summary>
        public readonly struct UserStatusUpdatedEvent
        {
            public readonly ChatChannel.ChannelId ChannelId;
            public readonly ChatChannel.ChatChannelType ChannelType;
            public readonly string UserId;
            public readonly bool IsOnline;

            public UserStatusUpdatedEvent(ChatChannel.ChannelId channelId, ChatChannel.ChatChannelType channelType, string userId, bool isOnline)
            {
                ChannelId = channelId;
                UserId = userId;
                IsOnline = isOnline;
                ChannelType = channelType;
            }
        }

        /// <summary>
        ///     Triggered By:   NearbyUserStateService
        ///     When:           Users' Status is changed in the batch when the LiveKit room connects/disconnects.
        /// </summary>
        public readonly struct ChannelUsersStatusUpdated
        {
            /// <summary>
            ///     Is <see cref="ChatChannel.EMPTY_CHANNEL_ID" /> if applied to all channels of the given <see cref="ChannelType" />
            /// </summary>
            public readonly ChatChannel.ChannelId ChannelId;
            public readonly ChatChannel.ChatChannelType ChannelType;
            public readonly ReadOnlyHashSet<string> OnlineUsers;

            public ChannelUsersStatusUpdated(ChatChannel.ChannelId channelId, ChatChannel.ChatChannelType channelType, ReadOnlyHashSet<string> onlineUsers)
            {
                ChannelId = channelId;
                OnlineUsers = onlineUsers;
                ChannelType = channelType;
            }

            public bool Qualifies(ChatChannel chatChannel)
            {
                // If applied to every USER Channel
                if (ChannelType == ChatChannel.ChatChannelType.USER && ChannelId.Equals(ChatChannel.EMPTY_CHANNEL_ID) && chatChannel.ChannelType == ChatChannel.ChatChannelType.USER)
                    return true;

                return chatChannel.Id.Equals(ChannelId);
            }
        }

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

#region General Chat Events
        /// <summary>
        ///     Event:          FocusRequestedEvent
        ///     Triggered By:   ChatInputPresenter
        ///     When:           The user clicks on the chat input field, signaling an intent to type.
        ///     Subscribers:    ChatFsmController: Transitions the UI to the FocusedChatState.
        /// </summary>
        public struct FocusRequestedEvent { }

        /// <summary>
        ///     Event:          CloseChatEvent
        ///     Triggered By:   ChatTitlebarPresenter
        ///     When:           The user clicks the 'X' button in the chat's title bar.
        ///     Subscribers:    ChatFsmController: Transitions the UI to the MinimizedChatState.
        /// </summary>
        public struct CloseChatEvent { }
#endregion

#region Miscellaneous Events
        /// <summary>
        ///     Event:          ToggleMembersEvent
        ///     Triggered By:   ChatTitlebarPresenter
        ///     When:           The user clicks the button to show or hide the member list for the current channel.
        ///     Subscribers:    ChatFsmController: Transitions the UI to/from the MembersChatState.
        /// </summary>
        public struct ToggleMembersEvent { }

        /// <summary>
        ///     Event:          CurrentChannelStateUpdatedEvent
        ///     Triggered By:   ChatUserStateBridge or ChatFsmController states.
        ///     When:           A real-time status change affects the current conversation OR the chat UI becomes visible again.
        ///     Subscribers:    ChatInputPresenter: Re-runs its permission checks for the current channel and updates the input view.
        /// </summary>
        public struct CurrentChannelStateUpdatedEvent { }
#endregion
    }
}
