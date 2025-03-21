using System.Collections.Generic;

namespace DCL.Chat.History
{
    public class ChatHistory : IChatHistory
    {
        public event IChatHistory.ChannelAddedDelegate ChannelAdded;
        public event IChatHistory.ChannelRemovedDelegate ChannelRemoved;
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
                    result += channel.Value.Messages.Count;

                return result;
            }
        }

        public ChatHistory()
        {
            AddOrGetChannel(ChatChannel.ChatChannelType.Nearby, ChatChannel.NEARBY_CHANNEL_ID);
        }

        public ChatChannel AddOrGetChannel(ChatChannel.ChatChannelType type, ChatChannel.ChannelId channelId)
        {
            if (channels.TryGetValue(channelId, out ChatChannel channel))
                return channel;

            ChatChannel newChannel = new ChatChannel(type, channelId.Id);
            newChannel.MessageAdded += (destinationChannel, addedMessage) => { MessageAdded?.Invoke(destinationChannel, addedMessage); };
            newChannel.Cleared += (clearedChannel) => { ChannelCleared?.Invoke(clearedChannel); };
            newChannel.ReadMessagesChanged += (changedChannel) => { ReadMessagesChanged?.Invoke(changedChannel); };

            channels.Add(newChannel.Id, newChannel);
            ChannelAdded?.Invoke(newChannel);
            return newChannel;
        }

        public void RemoveChannel(ChatChannel.ChannelId channelId)
        {
            ChatChannel channel = channels[channelId];
            channels.Remove(channelId);

            if(channel.ReadMessages != channel.Messages.Count)
                ReadMessagesChanged?.Invoke(channel);

            ChannelRemoved?.Invoke(channelId);
        }

        public void AddMessage(ChatChannel.ChannelId channelId, ChatMessage newMessage)
        {
            var channel = AddOrGetChannel(ChatChannel.ChatChannelType.User, channelId);
            channel.AddMessage(newMessage);
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
