using DCL.Diagnostics;
using System.Collections.Generic;
using System.Linq;

namespace DCL.Chat.History
{
    public class ChatHistory : IChatHistory
    {
        public event IChatHistory.ChannelAddedDelegate ChannelAdded;
        public event IChatHistory.ChannelRemovedDelegate ChannelRemoved;
        public event IChatHistory.ChannelClearedDelegate ChannelCleared;
        public event IChatHistory.MessageAddedDelegate MessageAdded;
        public event IChatHistory.ReadMessagesChangedDelegate ReadMessagesChanged;
        public event IChatHistory.AllChannelsRemovedDelegate AllChannelsRemoved;

        public IReadOnlyDictionary<ChatChannel.ChannelId, ChatChannel> Channels => channels;

        private readonly Dictionary<ChatChannel.ChannelId, ChatChannel> channels = new ();

        private int cachedReadMessages;
        private int cachedTotalMessages;
        private bool isReadMessagesDirty = true;
        private bool isTotalMessagesDirty = true;


        public int ReadMessages
        {
            get
            {
                if (!isReadMessagesDirty) return cachedReadMessages;

                cachedReadMessages = channels.Values.Sum(channel => channel.ReadMessages);
                isReadMessagesDirty = false;
                return cachedReadMessages;
            }
        }

        public int TotalMessages
        {
            get
            {
                if (!isTotalMessagesDirty) return cachedTotalMessages;

                cachedTotalMessages = channels.Values.Sum(channel => channel.Messages.Count);
                isTotalMessagesDirty = false;
                return cachedTotalMessages;
            }
        }

        public ChatChannel AddOrGetChannel(ChatChannel.ChannelId channelId, ChatChannel.ChatChannelType type)
        {
            if (channels.TryGetValue(channelId, out ChatChannel channel))
                return channel;

            if (type == ChatChannel.ChatChannelType.UNDEFINED)
            {
                ReportHub.LogError(ReportCategory.CHAT_MESSAGES, "Attempted to create a chat channel without specific type.");
                return null;
            }

            ChatChannel newChannel = new ChatChannel(type, channelId.Id);
            newChannel.MessageAdded += OnChannelMessageAdded;
            newChannel.Cleared += OnChannelCleared;
            newChannel.ReadMessagesChanged += OnChannelReadMessagesChanged;

            channels.Add(newChannel.Id, newChannel);
            ChannelAdded?.Invoke(newChannel);
            return newChannel;
        }

        public void RemoveChannel(ChatChannel.ChannelId channelId)
        {
            if (!channels.TryGetValue(channelId, out ChatChannel channel))
                return;

            UnsubscribeFromChannelEvents(channel);
            channels.Remove(channelId);
            isReadMessagesDirty = true;
            isTotalMessagesDirty = true;

            if(channel.ReadMessages != channel.Messages.Count)
                ReadMessagesChanged?.Invoke(channel);

            ChannelRemoved?.Invoke(channelId, channel.ChannelType);
        }

        public void AddMessage(ChatChannel.ChannelId channelId, ChatChannel.ChatChannelType? channelType, ChatMessage newMessage)
        {
            var channel = AddOrGetChannel(channelId, channelType ?? ChatChannel.ChatChannelType.UNDEFINED);
            channel.AddMessage(newMessage);
        }

        public void ClearChannel(ChatChannel.ChannelId channelId)
        {
            isReadMessagesDirty = true;
            isTotalMessagesDirty = true;

            if (channels.TryGetValue(channelId, out ChatChannel channel))
            {
                channel.Clear();
            }
        }

        public void DeleteAllChannels()
        {
            foreach (var channel in channels.Values)
            {
                UnsubscribeFromChannelEvents(channel);
                channel.Clear();
            }
            channels.Clear();
            isReadMessagesDirty = true;
            isTotalMessagesDirty = true;
        }

        private void OnChannelMessageAdded(ChatChannel destinationChannel, ChatMessage addedMessage)
        {
            isTotalMessagesDirty = true;
            MessageAdded?.Invoke(destinationChannel, addedMessage);
        }

        private void OnChannelCleared(ChatChannel clearedChannel)
        {
            isTotalMessagesDirty = true;
            ChannelCleared?.Invoke(clearedChannel);
        }

        private void OnChannelReadMessagesChanged(ChatChannel changedChannel)
        {
            isReadMessagesDirty = true;
            ReadMessagesChanged?.Invoke(changedChannel);
        }

        private void UnsubscribeFromChannelEvents(ChatChannel channel)
        {
            channel.MessageAdded -= OnChannelMessageAdded;
            channel.Cleared -= OnChannelCleared;
            channel.ReadMessagesChanged -= OnChannelReadMessagesChanged;
        }
    }
}
