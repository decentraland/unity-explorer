using Cysharp.Threading.Tasks;
using DCL.Chat;
using DCL.Multiplayer.Chats.Deduplication;
using DCL.Multiplayer.Connections.Messaging;
using DCL.Multiplayer.Connections.Messaging.Hubs;
using DCL.Multiplayer.Connections.Messaging.Pipe;
using DCL.Multiplayer.Connections.RoomHubs;
using DCL.Profiles;
using Decentraland.Kernel.Comms.Rfc4;
using LiveKit.Proto;
using LiveKit.Rooms;
using System;
using System.Threading;

namespace DCL.Multiplayer.Chats
{
    public class MultiplayerChatMessagesBus : IChatMessagesBus
    {
        private readonly IMessagePipesHub messagePipesHub;
        private readonly IRoomHub roomHub;
        private readonly IProfileRepository profileRepository;
        private readonly IMessageDeduplication messageDeduplication;
        private readonly CancellationTokenSource cancellationTokenSource = new ();

        public MultiplayerChatMessagesBus(IMessagePipesHub messagePipesHub, IRoomHub roomHub, IProfileRepository profileRepository, IMessageDeduplication messageDeduplication)
        {
            this.messagePipesHub = messagePipesHub;
            this.roomHub = roomHub;
            this.profileRepository = profileRepository;
            this.messageDeduplication = messageDeduplication;

            messagePipesHub.IslandPipe().Subscribe<Decentraland.Kernel.Comms.Rfc4.Chat>(Packet.MessageOneofCase.Chat, OnMessageReceived);
            messagePipesHub.ScenePipe().Subscribe<Decentraland.Kernel.Comms.Rfc4.Chat>(Packet.MessageOneofCase.Chat, OnMessageReceived);
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
                        false
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
            SendTo(message, timestamp, messagePipesHub.IslandPipe(), roomHub.IslandRoom());
            SendTo(message, timestamp, messagePipesHub.ScenePipe(), roomHub.SceneRoom());
        }

        private  void SendTo(string message, double timestamp, IMessagePipe messagePipe, IRoom room)
        {
            var chat = messagePipe.NewMessage<Decentraland.Kernel.Comms.Rfc4.Chat>();
            chat.Payload.Message = message;
            chat.Payload.Timestamp = timestamp;
            chat.AddRecipients(room);
            chat.SendAndDisposeAsync(cancellationTokenSource.Token, DataPacketKind.KindReliable).Forget();
        }

        public void Dispose()
        {
            cancellationTokenSource.Cancel();
            cancellationTokenSource.Dispose();
        }
    }
}
