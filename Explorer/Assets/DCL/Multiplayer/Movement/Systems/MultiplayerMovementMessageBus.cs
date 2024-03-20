using Arch.Core;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Multiplayer.Connections.Messaging;
using DCL.Multiplayer.Connections.Messaging.Hubs;
using DCL.Multiplayer.Connections.Messaging.Pipe;
using DCL.Multiplayer.Profiles.Tables;
using Decentraland.Kernel.Comms.Rfc4;
using System;
using System.Threading;
using UnityEngine;
using UnityEngine.Pool;
using Utility.Multithreading;
using Utility.PriorityQueue;
using static DCL.CharacterMotion.Components.CharacterAnimationComponent;

namespace DCL.Multiplayer.Movement.Systems
{
    public class MultiplayerMovementMessageBus : IDisposable
    {
        private readonly IMessagePipesHub messagePipesHub;
        private readonly IReadOnlyEntityParticipantTable entityParticipantTable;
        private readonly IObjectPool<SimplePriorityQueue<FullMovementMessage>> queuePool;
        private readonly CancellationTokenSource cancellationTokenSource = new ();
        private World globalWorld = null!;

        public MultiplayerMovementMessageBus(IMessagePipesHub messagePipesHub, IReadOnlyEntityParticipantTable entityParticipantTable, IObjectPool<SimplePriorityQueue<FullMovementMessage>> queuePool)
        {
            this.messagePipesHub = messagePipesHub;
            this.entityParticipantTable = entityParticipantTable;
            this.queuePool = queuePool;

            this.messagePipesHub.IslandPipe().Subscribe<Decentraland.Kernel.Comms.Rfc4.Movement>(Packet.MessageOneofCase.Movement, OnMessageReceived);
            this.messagePipesHub.ScenePipe().Subscribe<Decentraland.Kernel.Comms.Rfc4.Movement>(Packet.MessageOneofCase.Movement, OnMessageReceived);
        }

        private void OnMessageReceived(ReceivedMessage<Decentraland.Kernel.Comms.Rfc4.Movement> obj)
        {
            HandleAsync(obj).Forget();
        }

        public void Send(FullMovementMessage message)
        {
            WriteAndSend(message, messagePipesHub.IslandPipe());
            WriteAndSend(message, messagePipesHub.ScenePipe());
        }

        public void InjectWorld(World world)
        {
            this.globalWorld = world;
        }

        private void WriteAndSend(FullMovementMessage message, IMessagePipe messagePipe)
        {
            var messageWrap = messagePipe.NewMessage<Decentraland.Kernel.Comms.Rfc4.Movement>();
            WriteToProto(message, messageWrap.Payload);
            messageWrap.SendAndDisposeAsync(cancellationTokenSource.Token).Forget();
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

                if (cancellationTokenSource.Token.IsCancellationRequested)
                    return;

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

        private void Inbox(FullMovementMessage fullMovementMessage, string @for)
        {
            QueueFor(@for)?.Enqueue(fullMovementMessage, fullMovementMessage.timestamp);
        }

        private SimplePriorityQueue<FullMovementMessage>? QueueFor(string walletId)
        {
            if (entityParticipantTable.Has(walletId) == false)
            {
                ReportHub.LogWarning(ReportCategory.MULTIPLAYER_MOVEMENT, $"Entity for wallet {walletId} not found");
                return null;
            }

            var entity = entityParticipantTable.Entity(walletId);

            if (globalWorld.Has<SimplePriorityQueue<FullMovementMessage>>(entity) == false)
                globalWorld.Add(entity, queuePool.Get());

            return globalWorld.Get<SimplePriorityQueue<FullMovementMessage>>(entity);
        }

        public async UniTaskVoid SelfSendWithDelayAsync(FullMovementMessage message, float delay)
        {
            await UniTask.Delay(TimeSpan.FromSeconds(delay), cancellationToken: cancellationTokenSource.Token);
            Inbox(message, @for: RemotePlayerMovementComponent.TEST_ID);
        }

        public void Dispose()
        {
            cancellationTokenSource.Cancel();
            cancellationTokenSource.Dispose();
        }
    }
}
