#nullable enable
using Cysharp.Threading.Tasks;
using DCL.Chat.History;
using DCL.Chat.MessageBus.Deduplication;
using DCL.Diagnostics;
using DCL.Friends.UserBlocking;
using DCL.Multiplayer.Connections.Messaging;
using DCL.Multiplayer.Connections.Messaging.Hubs;
using DCL.Multiplayer.Connections.Messaging.Pipe;
using DCL.Utilities;
using LiveKit.Proto;
using System;
using System.Threading;

namespace DCL.Chat.MessageBus
{
    public class MultiplayerChatMessagesBus : IChatMessagesBus
    {
        private readonly IMessagePipesHub messagePipesHub;
        private readonly IMessageDeduplication<double> messageDeduplication;
        private readonly CancellationTokenSource cancellationTokenSource = new ();
        private readonly ObjectProxy<IUserBlockingCache> userBlockingCacheProxy;
        private readonly ChatMessageFactory messageFactory;

        public event Action<ChatChannel.ChannelId, ChatMessage>? MessageAdded;

        public MultiplayerChatMessagesBus(IMessagePipesHub messagePipesHub,
            ChatMessageFactory messageFactory,
            IMessageDeduplication<double> messageDeduplication,
            ObjectProxy<IUserBlockingCache> userBlockingCacheProxy)
        {
            this.messagePipesHub = messagePipesHub;
            this.messageDeduplication = messageDeduplication;
            this.userBlockingCacheProxy = userBlockingCacheProxy;
            this.messageFactory = messageFactory;

            messagePipesHub.IslandPipe().Subscribe<Decentraland.Kernel.Comms.Rfc4.Chat>(Decentraland.Kernel.Comms.Rfc4.Packet.MessageOneofCase.Chat, OnMessageReceived);
            messagePipesHub.ScenePipe().Subscribe<Decentraland.Kernel.Comms.Rfc4.Chat>(Decentraland.Kernel.Comms.Rfc4.Packet.MessageOneofCase.Chat, OnMessageReceived);
            messagePipesHub.ChatPipe().Subscribe<Decentraland.Kernel.Comms.Rfc4.Chat>(Decentraland.Kernel.Comms.Rfc4.Packet.MessageOneofCase.Chat, OnPrivateMessageReceived);
        }

        public void Dispose()
        {
            cancellationTokenSource.Cancel();
            cancellationTokenSource.Dispose();
        }

        private void OnPrivateMessageReceived(ReceivedMessage<Decentraland.Kernel.Comms.Rfc4.Chat> receivedMessage)
        {
            ReportHub.Log(ReportCategory.CHAT_MESSAGES, $"Received Private Message from {receivedMessage.FromWalletId}: {receivedMessage.Payload}");
            OnChatAsync(receivedMessage, true).Forget();
        }

        private void OnMessageReceived(ReceivedMessage<Decentraland.Kernel.Comms.Rfc4.Chat> receivedMessage)
        {
            OnChatAsync(receivedMessage).Forget();
        }

        private async UniTaskVoid OnChatAsync(ReceivedMessage<Decentraland.Kernel.Comms.Rfc4.Chat> receivedMessage, bool isPrivate = false)
        {
            using (receivedMessage)
            {
                if (messageDeduplication.TryPass(receivedMessage.FromWalletId, receivedMessage.Payload.Timestamp) == false
                    || IsUserBlockedAndMessagesHidden(receivedMessage.FromWalletId))
                    return;

                ChatChannel.ChannelId parsedChannelId = isPrivate? new ChatChannel.ChannelId(receivedMessage.FromWalletId) : ChatChannel.NEARBY_CHANNEL_ID;
                ChatMessage newMessage = messageFactory.CreateChatMessage(receivedMessage.FromWalletId, false, receivedMessage.Payload.Message, null);

                MessageAdded?.Invoke(parsedChannelId, newMessage);
            }
        }

        private bool IsUserBlockedAndMessagesHidden(string walletAddress) =>
            userBlockingCacheProxy.Configured && userBlockingCacheProxy.Object!.HideChatMessages && userBlockingCacheProxy.Object!.UserIsBlocked(walletAddress);

        public void Send(ChatChannel channel, string message, string origin)
        {
            if (cancellationTokenSource.IsCancellationRequested)
                throw new Exception("ChatMessagesBus is disposed");

            double timestamp = DateTime.UtcNow.TimeOfDay.TotalSeconds;
            switch (channel.ChannelType)
            {
                case ChatChannel.ChatChannelType.NEARBY:
                    SendTo(message, timestamp, messagePipesHub.IslandPipe());
                    SendTo(message, timestamp, messagePipesHub.ScenePipe());
                    break;
                case ChatChannel.ChatChannelType.USER:
                    SendTo(message, timestamp, messagePipesHub.ChatPipe(), channel.Id.Id);
                    break;
                default:
                    break;
            }

        }

        private void SendTo(string message, double timestamp, IMessagePipe messagePipe, string? recipient = null)
        {
            MessageWrap<Decentraland.Kernel.Comms.Rfc4.Chat> chat = messagePipe.NewMessage<Decentraland.Kernel.Comms.Rfc4.Chat>();
            if (recipient != null)
                chat.AddSpecialRecipient(recipient);
            chat.Payload.Message = message;
            chat.Payload.Timestamp = timestamp;
            chat.SendAndDisposeAsync(cancellationTokenSource.Token, DataPacketKind.KindReliable).Forget();
        }
    }
}
