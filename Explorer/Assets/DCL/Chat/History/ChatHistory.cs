using System.Collections.Generic;

namespace DCL.Chat.History
{
    public class ChatHistory : IChatHistory
    {
        public event IChatHistory.ChannelAddedDelegate ChannelAdded;
        public event IChatHistory.ChannelClearedDelegate ChannelCleared;
        public event IChatHistory.MessageAddedDelegate MessageAdded;
        public event IChatHistory.ReadMessagesChangedDelegate ReadMessagesChanged;

        private readonly Dictionary<ChatChannel.ChannelId, ChatChannel> channels = new ();

        public IReadOnlyDictionary<ChatChannel.ChannelId, ChatChannel> Channels => channels;

        public int ReadMessages
        {
            get
            {
                int result = 0;

                foreach (KeyValuePair<ChatChannel.ChannelId, ChatChannel> channel in channels)
                {
                    result += channel.Value.ReadMessages;
                }

                return result;
            }
        }

        public int TotalMessages
        {
            get
            {
                int result = 0;

                foreach (KeyValuePair<ChatChannel.ChannelId, ChatChannel> channel in channels)
                {
                    result += channel.Value.Messages.Count;
                }

                return result;
            }
        }

        public ChatHistory()
        {
            AddChannel(ChatChannel.ChatChannelType.NearBy, string.Empty);
        }

        public ChatChannel.ChannelId AddChannel(ChatChannel.ChatChannelType type, string channelName)
        {
            ChatChannel newChannel = new ChatChannel(type, channelName);
            newChannel.MessageAdded += (destinationChannel, addedMessage) => { MessageAdded?.Invoke(destinationChannel, addedMessage); };
            newChannel.Cleared += (clearedChannel) => { ChannelCleared?.Invoke(clearedChannel); };
            newChannel.ReadMessagesChanged += (changedChannel) => { ReadMessagesChanged?.Invoke(changedChannel); };

            channels.Add(newChannel.Id, newChannel);

            ChannelAdded?.Invoke(newChannel);

            return newChannel.Id;
        }

        public void RemoveChannel(ChatChannel.ChannelId channelId)
        {
            ChatChannel channel = channels[channelId];
            channels.Remove(channelId);

            if(channel.ReadMessages != channel.Messages.Count)
                ReadMessagesChanged?.Invoke(channel);
        }

        public void AddMessage(ChatChannel.ChannelId channelId, ChatMessage newMessage)
        {
            channels[channelId].AddMessage(newMessage);
        }

        public void ClearChannel(ChatChannel.ChannelId channelId)
        {
            channels[channelId].Clear();
        }

        public void ClearAllChannels()
        {
            foreach (var chatChannel in channels)
            {
                ClearChannel(chatChannel.Key);
            }
        }
    }
}
