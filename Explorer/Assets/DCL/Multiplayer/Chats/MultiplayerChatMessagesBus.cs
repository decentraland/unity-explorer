using Cysharp.Threading.Tasks;
using DCL.Chat;
using DCL.Multiplayer.Connections.Messaging;
using DCL.Multiplayer.Connections.Messaging.Hubs;
using DCL.Multiplayer.Connections.RoomHubs;
using DCL.Profiles;
using LiveKit.Proto;
using System;
using System.Threading;

namespace DCL.Multiplayer.Chats
{
    public class MultiplayerChatMessagesBus : IChatMessagesBus
    {
        private readonly IMessagePipesHub messagePipesHub;
        private readonly IRoomHub roomHub;
        private readonly IProfileRepository profileRepository;

        public MultiplayerChatMessagesBus(IMessagePipesHub messagePipesHub, IRoomHub roomHub, IProfileRepository profileRepository)
        {
            this.messagePipesHub = messagePipesHub;
            this.roomHub = roomHub;
            this.profileRepository = profileRepository;

            messagePipesHub.IslandPipe().Subscribe<Decentraland.Kernel.Comms.Rfc4.Chat>(OnMessageReceived);
            messagePipesHub.ScenePipe().Subscribe<Decentraland.Kernel.Comms.Rfc4.Chat>(OnMessageReceived);
        }

        private void OnMessageReceived(ReceivedMessage<Decentraland.Kernel.Comms.Rfc4.Chat> receivedMessage)
        {
            OnChatAsync(receivedMessage).Forget();
        }

        private async UniTaskVoid OnChatAsync(ReceivedMessage<Decentraland.Kernel.Comms.Rfc4.Chat> receivedMessage)
        {
            using (receivedMessage)
            {
                var profile = await profileRepository.GetAsync(receivedMessage.FromWalletId, 0, CancellationToken.None);

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
            var chat = messagePipesHub.IslandPipe().NewMessage<Decentraland.Kernel.Comms.Rfc4.Chat>();
            chat.Payload.Message = message;
            chat.Payload.Timestamp = DateTime.UtcNow.TimeOfDay.TotalSeconds;
            chat.AddRecipients(roomHub.IslandRoom());
            chat.SendAndDisposeAsync(DataPacketKind.KindReliable).Forget();
        }
    }
}
