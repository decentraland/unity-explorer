using Cysharp.Threading.Tasks;
using DCL.Multiplayer.Connections.Archipelago.Rooms;
using DCL.Multiplayer.Connections.Messaging;
using DCL.Multiplayer.Connections.Pools;
using DCL.Multiplayer.Connections.RoomHubs;
using DCL.Multiplayer.Movement.Settings;
using Decentraland.Kernel.Comms.Rfc4;
using Google.Protobuf;
using LiveKit.Internal.FFIClients.Pools;
using LiveKit.Internal.FFIClients.Pools.Memory;
using LiveKit.Proto;
using LiveKit.Rooms;
using LiveKit.Rooms.Participants;
using System;
using System.Collections.Generic;
using UnityEngine;
using Utility.PriorityQueue;
using static DCL.CharacterMotion.Components.CharacterAnimationComponent;
using Random = UnityEngine.Random;

namespace DCL.Multiplayer.Movement.System
{
    public class MultiplayerMovementMessageBus
    {
        private const string TOPIC = "movement";

        public readonly Dictionary<string, SimplePriorityQueue<FullMovementMessage>> InboxByParticipantMap = new ();

        private readonly IRoomHub roomHub;
        private IMultiplayerMovementSettings settings;

        private readonly IMemoryPool memoryPool;
        private readonly IMultiPool multiPool;
        private readonly MessageParser<Packet> packetParser;

        public MultiplayerMovementMessageBus(IRoomHub roomHub, IMemoryPool memoryPool, IMultiPool multiPool)
        {
            this.roomHub = roomHub;

            this.memoryPool = memoryPool;
            this.multiPool = multiPool;
            packetParser = new MessageParser<Packet>(multiPool.Get<Packet>);

            if (roomHub.IslandRoom() is IslandRoomMock)
            {
                roomHub.IslandRoom().DataPipe.DataReceived += InboxSelfMessageWithDelay;
                // roomHub.SceneRoom().DataPipe.DataReceived += InboxSelfMessageWithDelay;
            }
            else
            {
                roomHub.IslandRoom().DataPipe.DataReceived += InboxDeserializedMessage;
                roomHub.SceneRoom().DataPipe.DataReceived += InboxDeserializedMessage;
            }
        }

        ~MultiplayerMovementMessageBus()
        {
            if (roomHub.IslandRoom() is IslandRoomMock)
            {
                roomHub.IslandRoom().DataPipe.DataReceived -= InboxSelfMessageWithDelay;
                // roomHub.SceneRoom().DataPipe.DataReceived -= InboxSelfMessageWithDelay;
            }
            else
            {
                roomHub.IslandRoom().DataPipe.DataReceived -= InboxDeserializedMessage;
                roomHub.SceneRoom().DataPipe.DataReceived -= InboxDeserializedMessage;
            }
        }

        public void SetSettings(IMultiplayerMovementSettings settings)
        {
            this.settings = settings;
        }

        public void Send(Vector3 position, Vector3 velocity, AnimationStates animState, bool isStunned)
        {
            using var wrap = multiPool.TempResource<Packet>();
            var packet = wrap.value;

            using var chatWrap = multiPool.TempResource<Decentraland.Kernel.Comms.Rfc4.Movement>();
            packet.Movement = chatWrap.value;

            {
                packet.Movement.Timestamp = UnityEngine.Time.unscaledTime;

                packet.Movement.PositionX = position.x;
                packet.Movement.PositionY = position.y;
                packet.Movement.PositionZ = position.z;

                packet.Movement.VelocityX = velocity.x;
                packet.Movement.VelocityY = velocity.y;
                packet.Movement.VelocityZ = velocity.z;

                packet.Movement.MovementBlendValue = animState.MovementBlendValue;
                packet.Movement.SlideBlendValue = animState.SlideBlendValue;

                packet.Movement.IsGrounded = animState.IsGrounded;
                packet.Movement.IsJumping = animState.IsJumping;
                packet.Movement.IsLongJump = animState.IsLongJump;
                packet.Movement.IsFalling = animState.IsFalling;
                packet.Movement.IsLongFall = animState.IsLongFall;

                packet.Movement.IsStunned = isStunned;
            }

            using var memoryWrap = memoryPool.Memory(packet);
            packet.WriteTo(memoryWrap);

            Send(memoryWrap.Span());
        }

        private void Send(Span<byte> data)
        {
            Send(roomHub.IslandRoom(), data);
            Send(roomHub.SceneRoom(), data);
        }

        private static void Send(IRoom room, Span<byte> data)
        {
            room.DataPipe.PublishData(data, TOPIC, room.Participants.RemoteParticipantSids(), DataPacketKind.KindReliable);
        }

        private void InboxSelfMessageWithDelay(ReadOnlySpan<byte> data, Participant participant, DataPacketKind kind)
        {
            if (settings == null) return;

            FullMovementMessage? message = FullMovementMessageSerializer.DeserializeMessage(data);

            if (message != null)
                UniTask.Delay(TimeSpan.FromSeconds(settings.Latency + (settings.Latency * Random.Range(0, settings.LatencyJitter))))
                       .ContinueWith(() => Inbox(message.Value, @for: RemotePlayerMovementComponent.TEST_ID))
                       .Forget();
        }

        private void InboxDeserializedMessage(ReadOnlySpan<byte> data, Participant participant, DataPacketKind kind)
        {
            FullMovementMessage? message = FullMovementMessageSerializer.DeserializeMessage(data);

            if (message != null)
                Inbox(message.Value, @for: participant.Identity); // TODO (Vit): filter out Island messages if Participant is presented in the Room
        }

        private void Inbox(FullMovementMessage fullMovementMessage, string @for)
        {
            if (InboxByParticipantMap.TryGetValue(@for, out SimplePriorityQueue<FullMovementMessage>? queue) && !queue.Contains(fullMovementMessage))
                queue.Enqueue(fullMovementMessage, fullMovementMessage.timestamp);
            else
            {
                var newQueue = new SimplePriorityQueue<FullMovementMessage>(); // TODO (Vit): pooling
                newQueue.Enqueue(fullMovementMessage, fullMovementMessage.timestamp);

                InboxByParticipantMap.Add(@for, newQueue);
            }
        }
    }
}
