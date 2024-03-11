using Cysharp.Threading.Tasks;
using DCL.Chat;
using DCL.Diagnostics;
using DCL.Multiplayer.Connections.Messaging;
using DCL.Multiplayer.Connections.Pools;
using DCL.Multiplayer.Connections.RoomHubs;
using DCL.Multiplayer.Connections.Typing;
using DCL.Profiles;
using DCL.Utilities.Extensions;
using Decentraland.Kernel.Comms.Rfc4;
using Google.Protobuf;
using LiveKit.client_sdk_unity.Runtime.Scripts.Internal.FFIClients;
using LiveKit.Internal.FFIClients.Pools;
using LiveKit.Internal.FFIClients.Pools.Memory;
using LiveKit.Proto;
using LiveKit.Rooms;
using LiveKit.Rooms.Participants;
using System;
using System.Threading;
using Utility.Multithreading;

namespace DCL.Multiplayer.Chats
{
    public class MultiplayerChatMessagesBus : IChatMessagesBus
    {
        private readonly IRoomHub roomHub;
        private readonly IMemoryPool memoryPool;
        private readonly IMultiPool multiPool;
        private readonly IProfileRepository profileRepository;
        private readonly MessageParser<Packet> packetParser;

        private const string TOPIC = "chat";

        public MultiplayerChatMessagesBus(IRoomHub roomHub, IMemoryPool memoryPool, IMultiPool multiPool, IProfileRepository profileRepository)
        {
            this.roomHub = roomHub;
            this.memoryPool = memoryPool;
            this.multiPool = multiPool;
            this.profileRepository = profileRepository;
            packetParser = new MessageParser<Packet>(multiPool.Get<Packet>);

            this.roomHub.IslandRoom().DataPipe.DataReceived += DataPipeOnDataReceived;
            this.roomHub.SceneRoom().DataPipe.DataReceived += DataPipeOnDataReceived;
        }

        ~MultiplayerChatMessagesBus()
        {
            this.roomHub.IslandRoom().DataPipe.DataReceived -= DataPipeOnDataReceived;
            this.roomHub.SceneRoom().DataPipe.DataReceived -= DataPipeOnDataReceived;
        }

        public event Action<ChatMessage>? OnMessageAdded;

        public void Send(string message)
        {
            using var wrap = multiPool.TempResource<Packet>();
            var packet = wrap.value;

            using var chatWrap = multiPool.TempResource<Decentraland.Kernel.Comms.Rfc4.Chat>();
            packet.Chat = chatWrap.value;

            packet.Chat.Message = message;
            packet.Chat.Timestamp = DateTime.UtcNow.TimeOfDay.TotalSeconds;

            using var memoryWrap = memoryPool.Memory(packet);
            packet.WriteTo(memoryWrap);

            Send(memoryWrap.Span());
        }

        private void Send(Span<byte> data)
        {
            Send(roomHub.IslandRoom(), data);
        }

        private static void Send(IRoom room, Span<byte> data)
        {
            room.DataPipe.PublishData(data, TOPIC, room.Participants.RemoteParticipantSids(), DataPacketKind.KindReliable);
        }

        private void DataPipeOnDataReceived(ReadOnlySpan<byte> data, Participant participant, DataPacketKind kind)
        {
            if (TryParse(data, out Packet? response) == false)
                return;

            if (response!.MessageCase is Packet.MessageOneofCase.Chat)
                HandleAsync(new SmartWrap<Packet>(response, multiPool), participant).Forget();
        }

        private bool TryParse(ReadOnlySpan<byte> data, out Packet? packet)
        {
            try
            {
                packet = packetParser.ParseFrom(data).EnsureNotNull();
                return true;
            }
            catch (Exception e)
            {
                ReportHub.LogWarning(
                    ReportCategory.ARCHIPELAGO_REQUEST,
                    $"Someone sent invalid packet: {data.Length} {data.HexReadableString()} {e}"
                );

                packet = null;
                return false;
            }
        }

        private async UniTaskVoid HandleAsync(SmartWrap<Packet> packet, Participant participant)
        {
            using (packet)
            {
                await using var _ = await ExecuteOnMainThreadScope.NewScopeAsync();

                var profile = await profileRepository.GetAsync(participant.Identity, 0, CancellationToken.None);

                if (packet.value.Chat != null)
                    OnMessageAdded?.Invoke(
                        new ChatMessage(
                            packet.value.Chat.Message!,
                            profile?.DisplayName ?? participant.Name,
                            participant.Identity,
                            false
                        )
                    );
            }
        }
    }
}
