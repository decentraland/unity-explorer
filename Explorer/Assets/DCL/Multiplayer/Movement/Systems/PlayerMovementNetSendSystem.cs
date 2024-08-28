using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.Character.CharacterMotion.Components;
using DCL.CharacterMotion.Components;
using DCL.Diagnostics;
using DCL.Multiplayer.Movement.Settings;
using ECS.Abstract;
using UnityEngine;
using Random = UnityEngine.Random;

namespace DCL.Multiplayer.Movement.Systems
{
    [UpdateInGroup(typeof(PostRenderingSystemGroup))]
    [LogCategory(ReportCategory.MULTIPLAYER_MOVEMENT)]
    public partial class PlayerMovementNetSendSystem : BaseUnityLoopSystem
    {
        private const int MAX_MESSAGES_PER_SEC = 10; // 10 Hz == 10 [msg/sec]

        private const float POSITION_MOVE_EPSILON = 0.0001f; // 1 mm
        private const float VELOCITY_MOVE_EPSILON = 0.01f; // 1 cm/s

        private readonly MultiplayerMovementMessageBus messageBus;
        private readonly IMultiplayerMovementSettings settings;

        private float sendRate;

        public PlayerMovementNetSendSystem(World world, MultiplayerMovementMessageBus messageBus, IMultiplayerMovementSettings settings) : base(world)
        {
            this.messageBus = messageBus;
            this.settings = settings;

            sendRate = this.settings.MoveSendRate;
        }

        protected override void Update(float t)
        {
            SendPlayerNetMovementQuery(World, t);
        }

        [Query]
        private void SendPlayerNetMovement(
            [Data] float t,
            ref PlayerMovementNetworkComponent playerMovement,
            ref CharacterAnimationComponent anim,
            ref StunComponent stun,
            ref MovementInputComponent move,
            ref JumpInputComponent jump
        )
        {
            UpdateMessagePerSecondTimer(t, ref playerMovement);

            if (playerMovement.MessagesSentInSec >= MAX_MESSAGES_PER_SEC) return;

            if (playerMovement.IsFirstMessage)
            {
                SendMessage(ref playerMovement, in anim, in stun, in move);
                playerMovement.IsFirstMessage = false;
                return;
            }

            float timeDiff = UnityEngine.Time.unscaledTime - playerMovement.LastSentMessage.timestamp;

            if (playerMovement.LastSentMessage.animState.IsGrounded != anim.States.IsGrounded
                || playerMovement.LastSentMessage.animState.IsJumping != anim.States.IsJumping)
            {
                SendMessage(ref playerMovement, in anim, in stun, in move);
                return;
            }

            bool isMoving = IsMoving(playerMovement);

            if (isMoving && sendRate > settings.MoveSendRate)
                sendRate = settings.MoveSendRate;

            if (timeDiff > sendRate)
            {
                if (!isMoving && sendRate < settings.StandSendRate)
                    sendRate = Mathf.Min(2 * sendRate, settings.StandSendRate);

                SendMessage(ref playerMovement, in anim, in stun, in move);
            }

            return;

            bool IsMoving(PlayerMovementNetworkComponent playerMovement) =>
                Mathf.Abs(playerMovement.LastSentMessage.rotationY - playerMovement.Character.transform.eulerAngles.y) > 0.1f ||
                Vector3.SqrMagnitude(playerMovement.LastSentMessage.position - playerMovement.Character.transform.position) > POSITION_MOVE_EPSILON * POSITION_MOVE_EPSILON ||
                Vector3.SqrMagnitude(playerMovement.LastSentMessage.velocity - playerMovement.Character.velocity) > VELOCITY_MOVE_EPSILON * VELOCITY_MOVE_EPSILON;
        }

        private static void UpdateMessagePerSecondTimer(float t, ref PlayerMovementNetworkComponent playerMovement)
        {
            if (playerMovement.MessagesPerSecResetCooldown > 0)
                playerMovement.MessagesPerSecResetCooldown -= t;
            else
            {
                playerMovement.MessagesPerSecResetCooldown = 1; // 1 [sec]
                playerMovement.MessagesSentInSec = 0;
            }
        }

        private void SendMessage(ref PlayerMovementNetworkComponent playerMovement, in CharacterAnimationComponent animation, in StunComponent playerStunComponent, in MovementInputComponent movement)
        {
            playerMovement.MessagesSentInSec++;

            // We use this calculation instead of Character.velocity because, Character.velocity is 0 in some cases (moving platform)
            float dist = (playerMovement.Character.transform.position - playerMovement.LastSentMessage.position).magnitude;
            float speed = dist / (UnityEngine.Time.unscaledTime - playerMovement.LastSentMessage.timestamp);

            var tier = 0;

            while (tier < settings.VelocityTiers.Length && speed >= settings.VelocityTiers[tier])
                tier++;

            playerMovement.LastSentMessage = new NetworkMovementMessage
            {
                timestamp = UnityEngine.Time.unscaledTime,
                position = playerMovement.Character.transform.position,
                velocity = playerMovement.Character.velocity,
                velocitySqrMagnitude = playerMovement.Character.velocity.sqrMagnitude,

                rotationY = playerMovement.Character.transform.eulerAngles.y,
                tier = tier,

                isStunned = playerStunComponent.IsStunned,
                isSliding = animation.IsSliding,

                animState = new AnimationStates
                {
                    IsGrounded = animation.States.IsGrounded,
                    IsJumping = animation.States.IsJumping,
                    IsLongJump = animation.States.IsLongJump,
                    IsFalling = animation.States.IsFalling,
                    IsLongFall = animation.States.IsLongFall,

                    // We don't send blend values explicitly. It is calculated from MovementKind and IsSliding fields
                    SlideBlendValue = 0f,
                    MovementBlendValue = 0f,
                },

                movementKind = movement.Kind,
            };

            messageBus.Send(playerMovement.LastSentMessage);

            // Debug purposes. Simulate package lost when Running
            if (settings.SelfSending
                && movement.Kind != MovementKind.RUN // simulate package lost when Running
               ) { messageBus.SelfSendWithDelayAsync(playerMovement.LastSentMessage, settings.Latency + (settings.Latency * Random.Range(0, settings.LatencyJitter))).Forget(); }
        }
    }
}
