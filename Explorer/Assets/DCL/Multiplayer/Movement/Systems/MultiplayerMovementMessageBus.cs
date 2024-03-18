using Cysharp.Threading.Tasks;
using DCL.Multiplayer.Connections.Messaging;
using DCL.Multiplayer.Connections.Messaging.Hubs;
using DCL.Multiplayer.Connections.Messaging.Pipe;
using DCL.Multiplayer.Connections.RoomHubs;
using LiveKit.Rooms;
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
        public readonly Dictionary<string, SimplePriorityQueue<FullMovementMessage>> InboxByParticipantMap = new ();

        private readonly IMessagePipesHub messagePipesHub;
        private readonly IRoomHub roomHub;

        public MultiplayerMovementMessageBus(IMessagePipesHub messagePipesHub, IRoomHub roomHub)
        {
            this.messagePipesHub = messagePipesHub;
            this.roomHub = roomHub;

            this.messagePipesHub.IslandPipe().Subscribe<Decentraland.Kernel.Comms.Rfc4.Movement>(OnMessageReceived);
            this.messagePipesHub.ScenePipe().Subscribe<Decentraland.Kernel.Comms.Rfc4.Movement>(OnMessageReceived);
        }

        private void OnMessageReceived(ReceivedMessage<Decentraland.Kernel.Comms.Rfc4.Movement> obj)
        {
            HandleAsync(obj).Forget();
        }

        public void Send(FullMovementMessage message)
        {
            WriteAndSend(message, messagePipesHub.IslandPipe(), roomHub.IslandRoom());
            WriteAndSend(message, messagePipesHub.ScenePipe(), roomHub.SceneRoom());
        }

        private static void WriteAndSend(FullMovementMessage message, IMessagePipe messagePipe, IRoom room)
        {
            var messageWrap = messagePipe.NewMessage<Decentraland.Kernel.Comms.Rfc4.Movement>();
            WriteToProto(message, messageWrap.Payload);
            messageWrap.AddRecipients(room);
            messageWrap.SendAndDisposeAsync().Forget();
        }

        private static void WriteToProto(FullMovementMessage message, Decentraland.Kernel.Comms.Rfc4.Movement movement)
        {
            movement.Timestamp = UnityEngine.Time.unscaledTime;

            movement.PositionX = message.position.x;
            movement.PositionY = message.position.y;
            movement.PositionZ = message.position.z;

            movement.VelocityX = message.velocity.x;
            movement.VelocityY = message.velocity.y;
            movement.VelocityZ = message.velocity.z;

            movement.MovementBlendValue = message.animState.MovementBlendValue;
            movement.SlideBlendValue = message.animState.SlideBlendValue;

            movement.IsGrounded = message.animState.IsGrounded;
            movement.IsJumping = message.animState.IsJumping;
            movement.IsLongJump = message.animState.IsLongJump;
            movement.IsFalling = message.animState.IsFalling;
            movement.IsLongFall = message.animState.IsLongFall;

            movement.IsStunned = message.isStunned;
        }

        private async UniTaskVoid HandleAsync(ReceivedMessage<Decentraland.Kernel.Comms.Rfc4.Movement> obj)
        {
            using (obj)
            {
                await using ExecuteOnMainThreadScope _ = await ExecuteOnMainThreadScope.NewScopeAsync();
                // TODO (Vit): filter out Island messages if Participant is presented in the Room
                {
                    Decentraland.Kernel.Comms.Rfc4.Movement proto = obj.Payload;

                    var message = new FullMovementMessage
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

                    Inbox(message, obj.FromWalletId);
                }
            }
        }

        //TODO entity table
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

        public async UniTaskVoid SelfSendWithDelayAsync(FullMovementMessage message, float delay)
        {
            await UniTask.Delay(TimeSpan.FromSeconds(delay));
            Inbox(message, @for: RemotePlayerMovementComponent.TEST_ID);
        }
    }
}
