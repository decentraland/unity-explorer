using Cysharp.Threading.Tasks;
using DCL.Multiplayer.Connections.Archipelago.Rooms;
using DCL.Multiplayer.Connections.Rooms.Connective;
using DCL.Multiplayer.Movement.MessageBusMock;
using DCL.Multiplayer.Movement.Settings;
using LiveKit.Proto;
using LiveKit.Rooms.Participants;
using System;
using System.Collections.Generic;
using Random = UnityEngine.Random;

namespace DCL.Multiplayer.Movement.ECS.System
{
    public class ReplicasMovementInbox
    {
        private readonly Queue<MessageMock> incomingMessages = new ();
        private readonly IArchipelagoIslandRoom room;
        private readonly IMultiplayerSpatialStateSettings settings;
        private readonly bool useMock;

        private bool isSubscribed;

        public int Count => incomingMessages.Count;

        public ReplicasMovementInbox(IArchipelagoIslandRoom room, IMultiplayerSpatialStateSettings settings)
        {
            this.room = room;
            this.settings = settings;
        }

        ~ReplicasMovementInbox()
        {
            if (isSubscribed)
                room.Room().DataPipe.DataReceived -= OnDataReceived;
        }

        public async UniTask InitializeAsync()
        {
            await UniTask.WaitUntil( () => room.CurrentState() == IConnectiveRoom.State.Running);

            room.Room().DataPipe.DataReceived += OnDataReceived;
            isSubscribed = true;
        }

        public MessageMock Dequeue()
        {
            MessageMock message = incomingMessages.Dequeue();
            return message;
        }

        private void OnDataReceived(ReadOnlySpan<byte> data, Participant participant, DataPacketKind kind)
        {
            if (participant.Identity != "0xcdc4a418e58df3c4c0ed3e51d87912b27219b5b1" && participant.Identity != "0x05de05303eab867d51854e8b4fe03f7acb0624d9") return;

            MessageMock? message = MessageMockSerializer.DeserializeMessage(data);

            if (message == null)
                return;

            if (room is not IslandRoomMock)
            {
                incomingMessages.Enqueue(message);
                return;
            }

            // TODO: Remove this when the mock is removed
            UniTask.Delay(
                        TimeSpan.FromSeconds(settings.Latency
                                             + (settings.Latency * Random.Range(0, settings.LatencyJitter))
                                             + (settings.PackageSentRate * Random.Range(0, settings.PackagesJitter))))
                   .ContinueWith(() => { incomingMessages.Enqueue(message); })
                   .Forget();
        }
    }
}
