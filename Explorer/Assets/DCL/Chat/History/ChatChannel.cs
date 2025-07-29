using System;
using System.Collections.Generic;

namespace DCL.Chat.History
{
    /// <summary>
    /// Represents a conversation thread. The amount of people involved depends on the type of channel.
    /// </summary>
    public class ChatChannel
    {
        private static readonly ChatMessage PADDING_MESSAGE = ChatMessage.NewPaddingElement();

        /// <summary>
        /// The ID of the "near-by" channel, which is always the same.
        /// </summary>
        public static readonly ChannelId NEARBY_CHANNEL_ID = new (ChatChannelType.NEARBY.ToString());
        public static readonly ChannelId EMPTY_CHANNEL_ID = new ();
        public static readonly ChatChannel NEARBY_CHANNEL = new (ChatChannelType.NEARBY, ChatChannelType.NEARBY.ToString());

        private const string COMMUNITY_PREFIX = "community:";

        /// <summary>
        /// Creates a channel Id with the format expected for a community channel.
        /// </summary>
        /// <param name="communityId">The UUID of the community as it is stored in the database.</param>
        /// <returns>The new channel Id.</returns>
        public static ChannelId NewCommunityChannelId(string communityId) =>
            new (COMMUNITY_PREFIX + communityId);

        /// <summary>
        /// Checks if a channel Id belongs to a community channel or not.
        /// </summary>
        /// <param name="channelId">The channel id to check.</param>
        /// <returns>True if it belongs to a community channel; False otherwise.</returns>
        public static bool IsCommunityChannelId(ChannelId channelId) =>
            channelId.Id.StartsWith(COMMUNITY_PREFIX);

        /// <summary>
        /// Checks if a channel Id belongs to a community channel or not.
        /// </summary>
        /// <param name="channelId">The channel id to check.</param>
        /// <returns>True if it belongs to a community channel; False otherwise.</returns>
        public static bool IsCommunityChannelId(string channelId) =>
            channelId.StartsWith(COMMUNITY_PREFIX);

        /// <summary>
        /// Extracts the community Id from a community channel Id.
        /// </summary>
        /// <param name="channelId">The channel id from which to extract the community Id.</param>
        /// <returns>The Id of the community, without prefix or suffix.</returns>
        public static string GetCommunityIdFromChannelId(ChannelId channelId) =>
            channelId.Id.Substring(COMMUNITY_PREFIX.Length);

        /// <summary>
        /// The unique identifier of a chat channel.
        /// </summary>
        public readonly struct ChannelId : IEquatable<ChannelId>
        {
            public readonly string Id;

            public ChannelId(string id)
            {
                Id = id;
            }

            public bool Equals(ChannelId other) =>
                Id == other.Id;
        }

        public delegate void ClearedDelegate(ChatChannel clearedChannel);
        public delegate void MessageAddedDelegate(ChatChannel destinationChannel, ChatMessage addedMessage, int index);
        public delegate void ReadMessagesChangedDelegate(ChatChannel changedChannel);

        /// <summary>
        /// Raised when all the messages of the channel are deleted.
        /// </summary>
        public event ClearedDelegate? Cleared;

        /// <summary>
        /// Raised when a message is added to the channel.
        /// </summary>
        public event MessageAddedDelegate? MessageAdded;

        /// <summary>
        /// Raised when a message is read, added or removed.
        /// </summary>
        public event ReadMessagesChangedDelegate? ReadMessagesChanged;

        /// <summary>
        /// Gets all the messages contained in the thread. The first messages in the list are the latest added.
        /// </summary>
        public IReadOnlyList<ChatMessage> Messages => messages;

        /// <summary>
        /// Gets the unique ID of the channel.
        /// </summary>
        public ChannelId Id { get; }

        /// <summary>
        /// Gets the type of the channel (nearby, user, community...).
        /// </summary>
        public ChatChannelType ChannelType { get; }

        /// <summary>
        /// The amount of messages already read by the local participant in the chat.
        /// </summary>
        public int ReadMessages
        {
            get => readMessages;
            set
            {
                if (value != readMessages)
                {
                    readMessages = value;
                    ReadMessagesChanged?.Invoke(this);
                }
            }
        }

        private readonly List<ChatMessage> messages = new ();
        private int readMessages;
        private bool isInitialized;

        public ChatChannel(ChatChannelType channelType, string channelId)
        {
            Id = new ChannelId(channelId);
            ChannelType = channelType;
        }

        /// <summary>
        /// Stores a set of chat messages in the channel. This operation will not trigger any event.
        /// </summary>
        /// <param name="messagesToStore">The messages of the channel, in the order they were sent.</param>
        public void FillChannel(List<ChatMessage> messagesToStore)
        {
            messages.Clear();

            messages.Capacity = messagesToStore.Count + 2;

            // Adding two elements to count as top and bottom padding
            messages.Add(PADDING_MESSAGE);

            // Messages are added in inverse order
            for (int i = messagesToStore.Count - 1; i >= 0; --i)
                messages.Add(messagesToStore[i]);

            messages.Add(PADDING_MESSAGE);

            isInitialized = true;
        }

        /// <summary>
        /// Appends a new message to the channel.
        /// </summary>
        /// <param name="message">A message.</param>
        public void AddMessage(ChatMessage message)
        {
            TryInitializeChannel();

            // Insert new message after first padding element (index 1)
            messages.Insert(1, message);

            MessageAdded?.Invoke(this, message, 1);
        }

        public void TryInitializeChannel()
        {
            if (isInitialized)
                return;

            // Adding two elements to count as top and bottom padding
            messages.Add(PADDING_MESSAGE);
            messages.Add(PADDING_MESSAGE);
            readMessages = 2; // both paddings
            isInitialized = true;
        }

        /// <summary>
        /// Deletes all the messages of the channel.
        /// </summary>
        public void Clear()
        {
            messages.Clear();
            isInitialized = false;
            MarkAllMessagesAsRead();
            Cleared?.Invoke(this);
        }

        /// <summary>
        /// All messages will be considered as read by the local participant in the chat.
        /// </summary>
        public void MarkAllMessagesAsRead()
        {
            ReadMessages = messages.Count;
        }

        /// <summary>
        /// The type of channel which limits who can participate in the channel.
        /// </summary>
        public enum ChatChannelType
        {
            /// <summary>
            /// The channel in which all users in an island can participate.
            /// </summary>
            NEARBY,

            /// <summary>
            /// A channel in which a limited group of users can participate.
            /// </summary>
            COMMUNITY,

            /// <summary>
            /// A private channel in which the current player chats with another user.
            /// </summary>
            USER,

            UNDEFINED,
        }
    }
}
