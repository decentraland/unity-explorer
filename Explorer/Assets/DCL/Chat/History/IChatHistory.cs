using System.Collections.Generic;

namespace DCL.Chat.History
{
    /// <summary>
    /// Represents all the chat conversations of the current player.
    /// </summary>
    public interface IChatHistory
    {
        public delegate void AllChannelsRemovedDelegate();
        public delegate void ChannelAddedDelegate(ChatChannel addedChannel);
        public delegate void ChannelRemovedDelegate(ChatChannel.ChannelId removedChannel, ChatChannel.ChatChannelType channelType);
        public delegate void ChannelClearedDelegate(ChatChannel claredChannel);
        public delegate void MessageAddedDelegate(ChatChannel destinationChannel, ChatMessage addedMessage);
        public delegate void ReadMessagesChangedDelegate(ChatChannel changedChannel);

        /// <summary>
        /// Raised when a new channel is added.
        /// </summary>
        event ChannelAddedDelegate ChannelAdded;

        /// <summary>
        /// Raised when a new channel is removed.
        /// </summary>
        event ChannelRemovedDelegate ChannelRemoved;

        /// <summary>
        /// Raised when a channel is emptied.
        /// </summary>
        event ChannelClearedDelegate ChannelCleared;

        /// <summary>
        /// Raised when a message is added to a channel.
        /// </summary>
        event MessageAddedDelegate MessageAdded;

        /// <summary>
        /// Raised when a message is read, added or removed in any channel.
        /// </summary>
        event ReadMessagesChangedDelegate ReadMessagesChanged;

        /// <summary>
        /// Gets all the channels stored in the history.
        /// </summary>
        IReadOnlyDictionary<ChatChannel.ChannelId, ChatChannel> Channels {  get; }

        /// <summary>
        /// Gets the sum of all read messages in all channels.
        /// </summary>
        public int ReadMessages { get; }

        /// <summary>
        /// Gets the sum of all messages in all channels.
        /// </summary>
        public int TotalMessages { get; }

        /// <summary>
        /// Creates and stores a new channel.
        /// </summary>
        /// <param name="type">The type of the channel.</param>
        /// <param name="channelId">The unique name of the channel (for a given type).</param>
        /// <returns>
        /// The id of the new channel.
        /// </returns>
        public ChatChannel AddOrGetChannel(ChatChannel.ChannelId channelId, ChatChannel.ChatChannelType type = ChatChannel.ChatChannelType.UNDEFINED);

        /// <summary>
        /// Removes a channel along with its messages (which implies a change in the amount of read messages).
        /// </summary>
        /// <param name="channelId">The channel to remove.</param>
        public void RemoveChannel(ChatChannel.ChannelId channelId);

        /// <summary>
        /// Adds a new message to a channel.
        /// </summary>
        /// <param name="channelId">The id of the channel.</param>
        /// <param name="newMessage">The new message.</param>
        public void AddMessage(ChatChannel.ChannelId channelId, ChatMessage newMessage);

        /// <summary>
        /// Deletes all the messages in all the channels and then removes all the channels.
        /// </summary>
        public void DeleteAllChannels();

        /// <summary>
        /// Deletes all the messages in a channel.
        /// </summary>
        /// <param name="channelId">The id of the channel.</param>
        public void ClearChannel(ChatChannel.ChannelId channelId);
    }
}
