using DCL.Chat.History;
using DCL.Diagnostics;
using DCL.Web3.Identities;
using System;

namespace DCL.Chat.MessageBus
{
    public class SelfResendChatMessageBus : IChatMessagesBus
    {
        private readonly MultiplayerChatMessagesBus origin;
        private readonly IWeb3IdentityCache web3IdentityCache;
        private readonly ChatMessageFactory messageFactory;

        public event Action<ChatChannel.ChannelId, ChatChannel.ChatChannelType, ChatMessage> MessageAdded;

        public SelfResendChatMessageBus(MultiplayerChatMessagesBus origin, IWeb3IdentityCache web3IdentityCache, ChatMessageFactory messageFactory)
        {
            this.origin = origin;
            this.web3IdentityCache = web3IdentityCache;
            this.messageFactory = messageFactory;
            this.origin.MessageAdded += OriginOnOnMessageAdded;
        }

        ~SelfResendChatMessageBus()
        {
            origin.MessageAdded -= OriginOnOnMessageAdded;
        }

        public void Dispose()
        {
            origin.Dispose();
        }

        private void OriginOnOnMessageAdded(ChatChannel.ChannelId channelId, ChatChannel.ChatChannelType channelType, ChatMessage message)
        {
            MessageAdded?.Invoke(channelId, channelType, message);
        }

        public void Send(ChatChannel channel, string message, string origin, string topic)
        {
            this.origin.Send(channel, message, origin, topic);
            SendSelf(channel, message, topic);
        }

        private void SendSelf(ChatChannel channel, string chatMessage, string topic)
        {
            IWeb3Identity identity = web3IdentityCache.Identity;

            if (identity == null)
            {
                ReportHub.LogWarning(ReportCategory.COMMS_SCENE_HANDLER, "SelfResendChatMessageBus.Send: Identity is null, can't send message");
                return;
            }

            ChatMessage newMessage = messageFactory.CreateChatMessage(identity.Address, true, chatMessage, null, DateTime.UtcNow.ToOADate());

            MessageAdded?.Invoke(channel.Id, channel.ChannelType, newMessage);
        }

    }
}
