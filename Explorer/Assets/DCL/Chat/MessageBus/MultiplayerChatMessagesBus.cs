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
using Decentraland.Kernel.Comms.Rfc4;
using LiveKit.Proto;
using System;
using System.Threading;
using Utility.PriorityQueue;
using MessageStamp = DCL.Multiplayer.Deduplication.MessageDeduplication<double>.RegisteredStamp;

namespace DCL.Chat.MessageBus
{
    public class MultiplayerChatMessagesBus : IChatMessagesBus
    {
        private readonly IMessagePipesHub messagePipesHub;
        private readonly IMessageDeduplication<double> messageDeduplication;
        private readonly CancellationTokenSource cancellationTokenSource = new ();
        private readonly ObjectProxy<IUserBlockingCache> userBlockingCacheProxy;
        private readonly ChatMessageFactory messageFactory;

        private readonly SimplePriorityQueue<MessageStamp, double> orderedStamps = new ();

        private readonly HeadOfLineBlockingDetection islandHeadOfLineBlockingDetection = new ("island");
        private readonly HeadOfLineBlockingDetection sceneHeadOfLineBlockingDetection = new ("scene");

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

            messagePipesHub.IslandPipe().Subscribe<Decentraland.Kernel.Comms.Rfc4.Chat>(Packet.MessageOneofCase.Chat, m => OnMessageReceived(m, islandHeadOfLineBlockingDetection));
            messagePipesHub.ScenePipe().Subscribe<Decentraland.Kernel.Comms.Rfc4.Chat>(Packet.MessageOneofCase.Chat, m => OnMessageReceived(m, sceneHeadOfLineBlockingDetection));
            messagePipesHub.ChatPipe().Subscribe<Decentraland.Kernel.Comms.Rfc4.Chat>(Packet.MessageOneofCase.Chat, OnPrivateMessageReceived);
        }

        public void Dispose()
        {
            cancellationTokenSource.Cancel();
            cancellationTokenSource.Dispose();
        }

        private void OnPrivateMessageReceived(ReceivedMessage<Decentraland.Kernel.Comms.Rfc4.Chat> receivedMessage)
        {
            ReportHub.Log(ReportCategory.CHAT_MESSAGES, $"Received Private Message from {receivedMessage.FromWalletId}: {receivedMessage.Payload}");
            OnChatAsync(receivedMessage, null, true).Forget();
        }

        private void OnMessageReceived(ReceivedMessage<Decentraland.Kernel.Comms.Rfc4.Chat> receivedMessage, HeadOfLineBlockingDetection headOfLineBlockingDetection)
        {
            OnChatAsync(receivedMessage, headOfLineBlockingDetection).Forget();
        }

        private async UniTaskVoid OnChatAsync(
            ReceivedMessage<Decentraland.Kernel.Comms.Rfc4.Chat> receivedMessage,
            HeadOfLineBlockingDetection? headOfLineBlockingDetection,
            bool isPrivate = false)
        {
            using (receivedMessage)
            {
                ReportHub.Log(ReportCategory.CHAT_MESSAGES,$"RAW MSG IN: From={receivedMessage.FromWalletId}, TS={receivedMessage.Payload.Timestamp}, Content='{receivedMessage.Payload.Message.Substring(0, Math.Min(20, receivedMessage.Payload.Message.Length))}'");

                headOfLineBlockingDetection?.RecordAndDetect(receivedMessage);

                if (messageDeduplication.TryPass(receivedMessage.FromWalletId, receivedMessage.Payload.Timestamp) == false
                    || IsUserBlockedAndMessagesHidden(receivedMessage.FromWalletId))
                {
                    ReportHub.Log(ReportCategory.CHAT_MESSAGES,$"Dedupe Check: Passed=false, From={receivedMessage.FromWalletId}, TS={receivedMessage.Payload.Timestamp}");
                    return;
                }
                else
                {
                    ReportHub.Log(ReportCategory.CHAT_MESSAGES,$"Dedupe Check: Passed=true, From={receivedMessage.FromWalletId}, TS={receivedMessage.Payload.Timestamp}");
                }

                ChatChannel.ChannelId parsedChannelId = isPrivate? new ChatChannel.ChannelId(receivedMessage.FromWalletId) : ChatChannel.NEARBY_CHANNEL_ID;

                var messageStamp = new MessageStamp(receivedMessage.FromWalletId, receivedMessage.Payload.Timestamp);

                // There is no problem with ordering of private messages as `await messageFactory.CreateChatMessageAsync` will always resolve the same wallet id
                if (!isPrivate)
                    orderedStamps.Enqueue(messageStamp, receivedMessage.Payload.Timestamp);

                ReportHub.Log(ReportCategory.CHAT_MESSAGES,$"BUS FIRING MessageAdded: From={receivedMessage.FromWalletId}, TS={receivedMessage.Payload.Timestamp}");
                ChatMessage newMessage;

                try
                {
                    newMessage = messageFactory.CreateChatMessage(receivedMessage.FromWalletId, false, receivedMessage.Payload.Message, null);

                    ReportHub.Log(ReportCategory.CHAT_MESSAGES, $"BUS FIRING MessageResolved: From={receivedMessage.FromWalletId}, TS={receivedMessage.Payload.Timestamp}");

                    if (!isPrivate)
                    {
                        // Make sure that the message that was resolved [first] is actually the message that was received first
                        while (!messageStamp.Equals(orderedStamps.First))
                            await UniTask.Yield(cancellationTokenSource.Token);
                    }
                }
                finally
                {
                    if (!isPrivate)
                        orderedStamps.Dequeue();
                }

                ReportHub.Log(ReportCategory.CHAT_MESSAGES, $"BUS FIRING MessageDispatching: From={receivedMessage.FromWalletId}, TS={receivedMessage.Payload.Timestamp}");

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
