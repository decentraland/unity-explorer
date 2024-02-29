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
            if (room is not IslandRoomMock)
            {
                incomingMessages.Enqueue(MessageMockSerializer.DeserializeMessage(data));
                return;
            }

            // TODO: Remove this when the mock is removed
            MessageMock message = MessageMockSerializer.DeserializeMessage(data);
            UniTask.Delay(
                        TimeSpan.FromSeconds(settings.Latency
                                             + (settings.Latency * Random.Range(0, settings.LatencyJitter))
                                             + (settings.PackageSentRate * Random.Range(0, settings.PackagesJitter))))
                   .ContinueWith(() => { incomingMessages.Enqueue(message); })
                   .Forget();
        }
    }
}
