using System;
using System.Collections.Generic;

namespace DCL.Chat.History
{
    /// <summary>
    /// Represents a conversation thread. The amount of people involved depends on the type of channel.
    /// </summary>
    public class ChatChannel
    {
        /// <summary>
        /// The type of channel which limits who can participate in the channel.
        /// </summary>
        public enum ChatChannelType
        {
            /// <summary>
            /// The channel in which all users in an island can participate.
            /// </summary>
            NearBy,

            /// <summary>
            /// A channel in which a limited group of users can participate.
            /// </summary>
            Community,

            /// <summary>
            /// A private channel in which the current player chats with another user.
            /// </summary>
            User
        }

        /// <summary>
        /// The unique identifier of a chat channel.
        /// </summary>
        public struct ChannelId
        {
            public string Id { get; }

            public ChannelId(ChatChannelType type, string name)
            {
                Id = type + ":" + name;
            }

            public static void GetTypeAndNameFromId(string id, out ChatChannelType channelType, out string channelName)
            {
                channelName = id.Substring(id.LastIndexOf(':') + 1);
                string channelIdType = id.Substring(0, id.LastIndexOf(':'));
                Enum.TryParse(channelIdType, out channelType);
            }
        }

        /// <summary>
        /// The ID of the "near-by" channel, which is always the same.
        /// </summary>
        public static readonly ChannelId NEARBY_CHANNEL = new (ChatChannelType.NearBy, string.Empty);

        public delegate void ClearedDelegate(ChatChannel clearedChannel);
        public delegate void MessageAddedDelegate(ChatChannel destinationChannel, ChatMessage addedMessage);
        public delegate void ReadMessagesChangedDelegate(ChatChannel changedChannel);

        /// <summary>
        /// Raised when all the messages of the channel are deleted.
        /// </summary>
        public event ClearedDelegate Cleared;

        /// <summary>
        /// Raised when a message is added to the channel.
        /// </summary>
        public event MessageAddedDelegate MessageAdded;

        /// <summary>
        /// Raised when a message is read, added or removed.
        /// </summary>
        public event ReadMessagesChangedDelegate ReadMessagesChanged;

        /// <summary>
        /// Gets all the messages contained in the thread. The first messages in the list are the latest added.
        /// </summary>
        public IReadOnlyList<ChatMessage> Messages => messages;

        /// <summary>
        /// The unique ID of the channel.
        /// </summary>
        public ChannelId Id { get; }

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

        public ChatChannel(ChatChannelType channelType, string channelName)
        {
            Id = new ChannelId(channelType, channelName);
        }

        /// <summary>
        /// Appends a new message to the channel.
        /// </summary>
        /// <param name="message">A message.</param>
        public void AddMessage(ChatMessage message)
        {
            // TODO: It makes no sense to store padding stuff in the chat data
            if (messages.Count is 0)
            {
                // Adding two elements to count as top and bottom padding
                messages.Add(new ChatMessage(true));
                messages.Add(new ChatMessage(true));
                readMessages = 2; // both paddings
            }

            // Removing padding element and reversing list due to infinite scroll view behaviour
            messages.Remove(messages[^1]);
            messages.Reverse();
            messages.Add(message);
            messages.Add(new ChatMessage(true));
            messages.Reverse();

            MessageAdded?.Invoke(this, message);
        }

        /// <summary>
        /// Deletes all the messages of the channel.
        /// </summary>
        public void Clear()
        {
            messages.Clear();
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
    }
}
