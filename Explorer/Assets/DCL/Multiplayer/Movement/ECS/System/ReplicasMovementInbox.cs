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
    public class RemotePlayersMovementInbox
    {
        private readonly Queue<MessageMock> incomingMessages = new ();
        private readonly IArchipelagoIslandRoom room;
        private readonly IMultiplayerSpatialStateSettings settings;
        private readonly bool useMock;

        public readonly Dictionary<string, Queue<MessageMock>> InboxByParticipantMap = new ();

        private bool isSubscribed;

        private MessageMock lastMessage;

        public RemotePlayersMovementInbox(IArchipelagoIslandRoom room, IMultiplayerSpatialStateSettings settings)
        {
            this.room = room;
            this.settings = settings;
        }

        ~RemotePlayersMovementInbox()
        {
            if (isSubscribed)
                room.Room().DataPipe.DataReceived -= OnDataReceived;
        }

        public async UniTask InitializeAsync()
        {
            await UniTask.WaitUntil(() => room.CurrentState() == IConnectiveRoom.State.Running);

            room.Room().DataPipe.DataReceived += OnDataReceived;
            isSubscribed = true;
        }

        private void OnDataReceived(ReadOnlySpan<byte> data, Participant participant, DataPacketKind kind)
        {
            MessageMock? message = MessageMockSerializer.DeserializeMessage(data);

            if (room is IslandRoomMock)
            {
                float sentRate = lastMessage == null ? 1f : message.timestamp - lastMessage.timestamp;
                lastMessage = message;

                UniTask.Delay(
                            TimeSpan.FromSeconds(settings.Latency
                                                 + (settings.Latency * Random.Range(0, settings.LatencyJitter))
                                                 + (sentRate * Random.Range(0, settings.PackagesJitter))))
                       .ContinueWith(() =>
                        {
                            Inbox(message, @for: RemotePlayerMovementComponent.SELF_ID);
                        })
                       .Forget();
            }
            else
            {
                if (message == null)
                    return;

                Inbox(message, @for: participant.Identity);
            }
        }

        private void Inbox(MessageMock message, string @for)
        {
            if (InboxByParticipantMap.TryGetValue(@for, out var queue))
                queue.Enqueue(message);
            else
            {
                var newQueue = new Queue<MessageMock>();
                newQueue.Enqueue(message);

                InboxByParticipantMap.Add(@for, newQueue);
            }
        }
    }
}
