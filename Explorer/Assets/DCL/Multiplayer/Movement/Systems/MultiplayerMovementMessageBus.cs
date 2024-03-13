using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Multiplayer.Connections.Messaging;
using DCL.Multiplayer.Connections.Pools;
using DCL.Multiplayer.Connections.RoomHubs;
using DCL.Multiplayer.Connections.Typing;
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
using System.Collections.Generic;
using UnityEngine;
using Utility.Multithreading;
using Utility.PriorityQueue;
using static DCL.CharacterMotion.Components.CharacterAnimationComponent;

namespace DCL.Multiplayer.Movement.System
{
    public class MultiplayerMovementMessageBus
    {
        private const string TOPIC = "movement";

        public readonly Dictionary<string, SimplePriorityQueue<FullMovementMessage>> InboxByParticipantMap = new ();

        private readonly IRoomHub roomHub;

        private readonly IMemoryPool memoryPool;
        private readonly IMultiPool multiPool;
        private readonly MessageParser<Packet> packetParser;

        public MultiplayerMovementMessageBus(IRoomHub roomHub, IMemoryPool memoryPool, IMultiPool multiPool)
        {
            this.roomHub = roomHub;

            this.memoryPool = memoryPool;
            this.multiPool = multiPool;
            packetParser = new MessageParser<Packet>(multiPool.Get<Packet>);

            roomHub.IslandRoom().DataPipe.DataReceived += InboxMessage;
            roomHub.SceneRoom().DataPipe.DataReceived += InboxMessage;
        }

        ~MultiplayerMovementMessageBus()
        {
            roomHub.IslandRoom().DataPipe.DataReceived -= InboxMessage;
            roomHub.SceneRoom().DataPipe.DataReceived -= InboxMessage;
        }

        private void InboxMessage(ReadOnlySpan<byte> data, Participant participant, DataPacketKind kind)
        {
            if (TryParse(data, out Packet? response) == false)
                return;

            if (response!.MessageCase is Packet.MessageOneofCase.Movement)
                HandleAsync(new SmartWrap<Packet>(response, multiPool), participant).Forget();
        }

        public void Send(Vector3 position, Vector3 velocity, AnimationStates animState, bool isStunned)
        {
            using SmartWrap<Packet> wrap = multiPool.TempResource<Packet>();
            Packet? packet = wrap.value;

            using SmartWrap<Decentraland.Kernel.Comms.Rfc4.Movement> moveWrap = multiPool.TempResource<Decentraland.Kernel.Comms.Rfc4.Movement>();
            packet.Movement = moveWrap.value;

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

            using MemoryWrap memoryWrap = memoryPool.Memory(packet);
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
            room.DataPipe.PublishData(data, TOPIC, room.Participants.RemoteParticipantSids());
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

                if (packet.value.Movement != null) // TODO (Vit): filter out Island messages if Participant is presented in the Room
                {
                    Decentraland.Kernel.Comms.Rfc4.Movement proto = packet.value.Movement;

                    FullMovementMessage message = new FullMovementMessage
                    {
                        timestamp = proto.Timestamp,
                        position = new Vector3(proto.PositionX, proto.PositionY, proto.PositionZ),
                        velocity = new Vector3(proto.VelocityX, proto.VelocityY, proto.VelocityZ),
                        animState = new AnimationStates
                        {
                            MovementBlendValue = proto.MovementBlendValue,
                            SlideBlendValue = proto.SlideBlendValue,
                            IsGrounded = proto.IsGrounded,
                            IsJumping = proto.IsJumping,
                            IsLongJump = proto.IsLongJump,
                            IsFalling = proto.IsFalling,
                            IsLongFall = proto.IsLongFall,
                        },
                        isStunned = proto.IsStunned,
                    };

                    Inbox(message, participant.Identity);
                }
            }
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

        // private void InboxSelfMessageWithDelay(ReadOnlySpan<byte> data, Participant participant, DataPacketKind kind)
        // {
        //     if (settings == null) return;
        //
        //     FullMovementMessage? message = FullMovementMessageSerializer.DeserializeMessage(data);
        //
        //     if (message != null)
        //         UniTask.Delay(TimeSpan.FromSeconds(settings.Latency + (settings.Latency * Random.Range(0, settings.LatencyJitter))))
        //                .ContinueWith(() => Inbox(message.Value, @for: RemotePlayerMovementComponent.TEST_ID))
        //                .Forget();
        // }

    }
}
