using Cysharp.Threading.Tasks;
using DCL.Chat.MessageBus.Deduplication;
using DCL.Multiplayer.Connections.Messaging;
using DCL.Multiplayer.Connections.Messaging.Hubs;
using DCL.Multiplayer.Connections.Messaging.Pipe;
using Decentraland.Kernel.Comms.Rfc4;
using DCL.Profiles;
using LiveKit.Proto;
using System;
using System.Threading;

namespace DCL.Chat.MessageBus
{
    public class MultiplayerChatMessagesBus : IChatMessagesBus
    {
        private readonly IMessagePipe messagePipe;
        private readonly IProfileRepository profileRepository;
        private readonly IMessageDeduplication<double> messageDeduplication;
        private readonly CancellationTokenSource cancellationTokenSource = new ();

        public MultiplayerChatMessagesBus(IMessagePipesHub messagePipesHub, IProfileRepository profileRepository, IMessageDeduplication<double> messageDeduplication)
            : this(messagePipesHub.ScenePipe(), profileRepository, messageDeduplication) { }

        public MultiplayerChatMessagesBus(IMessagePipe messagePipe, IProfileRepository profileRepository, IMessageDeduplication<double> messageDeduplication)
        {
            this.messagePipe = messagePipe;
            this.profileRepository = profileRepository;
            this.messageDeduplication = messageDeduplication;
            messagePipe.Subscribe<Decentraland.Kernel.Comms.Rfc4.Chat>(Packet.MessageOneofCase.Chat, OnMessageReceived);
        }

        private void OnMessageReceived(ReceivedMessage<Decentraland.Kernel.Comms.Rfc4.Chat> receivedMessage)
        {
            OnChatAsync(receivedMessage).Forget();
        }

        private async UniTaskVoid OnChatAsync(ReceivedMessage<Decentraland.Kernel.Comms.Rfc4.Chat> receivedMessage)
        {
            using (receivedMessage)
            {
                if (messageDeduplication.TryPass(receivedMessage.FromWalletId, receivedMessage.Payload.Timestamp) == false)
                    return;

                var profile = await profileRepository.GetAsync(receivedMessage.FromWalletId, 0, cancellationTokenSource.Token);

                OnMessageAdded?.Invoke(
                    new ChatMessage(
                        receivedMessage.Payload.Message!,
                        profile?.DisplayName ?? string.Empty,
                        receivedMessage.FromWalletId,
                        false,
                        true
                    )
                );
            }
        }

        public event Action<ChatMessage>? OnMessageAdded;

        public void Send(string message)
        {
            if (cancellationTokenSource.IsCancellationRequested)
                throw new Exception("ChatMessagesBus is disposed");

            double timestamp = DateTime.UtcNow.TimeOfDay.TotalSeconds;
            SendTo(message, timestamp, messagePipe);
        }

        private void SendTo(string message, double timestamp, IMessagePipe messagePipe)
        {
            var chat = messagePipe.NewMessage<Decentraland.Kernel.Comms.Rfc4.Chat>();
            chat.Payload.Message = message;
            chat.Payload.Timestamp = timestamp;
            chat.SendAndDisposeAsync(cancellationTokenSource.Token, DataPacketKind.KindReliable).Forget();
        }

        public void Dispose()
        {
            cancellationTokenSource.Cancel();
            cancellationTokenSource.Dispose();
        }
    }
}
