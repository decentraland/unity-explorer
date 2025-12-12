using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.AvatarRendering.Emotes;
using DCL.Character.CharacterMotion.Components;
using DCL.CharacterMotion.Components;
using DCL.Diagnostics;
using DCL.Multiplayer.Movement.Settings;
using DCL.Prefs;
using ECS.Abstract;
using System;
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
        private const float HEAD_IK_EPSILON = 1; // 1 deg

        private readonly MultiplayerMovementMessageBus messageBus;
        private readonly MultiplayerMovementSettings settings;
        private readonly MultiplayerDebugSettings debugSettings;

        private float sendRate;

        public PlayerMovementNetSendSystem(World world, MultiplayerMovementMessageBus messageBus, MultiplayerMovementSettings settings,
            MultiplayerDebugSettings debugSettings) : base(world)
        {
            this.messageBus = messageBus;
            this.settings = settings;
            this.debugSettings = debugSettings;

            sendRate = this.settings.MoveSendRate;
        }

        protected override void Update(float t)
        {
            SendPlayerNetMovementQuery(World, t);
        }

        [Query]
        private void SendPlayerNetMovement(
            [Data] float t,
            in Entity entity,
            ref PlayerMovementNetworkComponent playerMovement,
            ref CharacterAnimationComponent anim,
            ref StunComponent stun,
            ref MovementInputComponent move,
            in CharacterEmoteComponent emote,
            in HeadIKComponent headIK
        )
        {
            UpdateMessagePerSecondTimer(t, ref playerMovement);

            if (playerMovement.MessagesSentInSec >= MAX_MESSAGES_PER_SEC) return;

            if (playerMovement.IsFirstMessage)
            {
                SendMessage(ref playerMovement, in anim, in stun, in move, in headIK, emote.IsPlayingEmote, isInstant: true);
                playerMovement.IsFirstMessage = false;
                return;
            }

            float timeDiff = UnityEngine.Time.unscaledTime - playerMovement.LastSentMessage.timestamp;

            bool justTeleported = World.Has<PlayerTeleportIntent.JustTeleported>(entity) || World.Has<PlayerTeleportIntent.JustTeleportedLocally>(entity);

            if (playerMovement.LastSentMessage.animState.IsGrounded != anim.States.IsGrounded
                || playerMovement.LastSentMessage.animState.IsJumping != anim.States.IsJumping)
            {
                SendMessage(ref playerMovement, in anim, in stun, in move, in headIK, emote.IsPlayingEmote, justTeleported);
                return;
            }

            bool anythingChanged = AnythingChanged(playerMovement, headIK);

            if (anythingChanged && sendRate > settings.MoveSendRate)
                sendRate = settings.MoveSendRate;

            if (timeDiff > sendRate)
            {
                if (!anythingChanged && sendRate < settings.StandSendRate)
                    sendRate = Mathf.Min(2 * sendRate, settings.StandSendRate);

                SendMessage(ref playerMovement, in anim, in stun, in move, in headIK, emote.IsPlayingEmote, justTeleported);
            }

            if(World.Has<PlayerTeleportIntent.JustTeleportedLocally>(entity))
                // Note: It can't be removed at this point because there may send another message instantly which would not be marked as instant
                World.Get<PlayerTeleportIntent.JustTeleportedLocally>(entity).IsConsumed = true;

            return;

            bool AnythingChanged(PlayerMovementNetworkComponent playerMovement, in HeadIKComponent headIK)
            {
                NetworkMovementMessage snapshot = playerMovement.LastSentMessage;
                Vector2 currentHeadYawAndPitch = headIK.GetHeadYawAndPitch();

                return Mathf.Abs(snapshot.rotationY - playerMovement.Character.transform.eulerAngles.y) > 0.1f ||
                       Vector3.SqrMagnitude(snapshot.position - playerMovement.Character.transform.position) > POSITION_MOVE_EPSILON * POSITION_MOVE_EPSILON ||
                       Vector3.SqrMagnitude(snapshot.velocity - playerMovement.Character.velocity) > VELOCITY_MOVE_EPSILON * VELOCITY_MOVE_EPSILON ||
                       snapshot.headIKEnabled != headIK.IsEnabled ||
                       Math.Abs(snapshot.headYawAndPitch.x - currentHeadYawAndPitch.x) > HEAD_IK_EPSILON ||
                       Math.Abs(snapshot.headYawAndPitch.y - currentHeadYawAndPitch.y) > HEAD_IK_EPSILON;
            }
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

        private void SendMessage(ref PlayerMovementNetworkComponent playerMovement,
            in CharacterAnimationComponent animation,
            in StunComponent playerStunComponent,
            in MovementInputComponent input,
            in HeadIKComponent headIK,
            bool isEmoting,
            bool isInstant)
        {
            playerMovement.MessagesSentInSec++;

            // We use this calculation instead of Character.velocity because, Character.velocity is 0 in some cases (moving platform)
            float dist = (playerMovement.Character.transform.position - playerMovement.LastSentMessage.position).magnitude;
            float speed = dist / (UnityEngine.Time.unscaledTime - playerMovement.LastSentMessage.timestamp);

            byte velocityTier = VelocityTierFromSpeed(speed);

            bool headSyncEnabled = DCLPlayerPrefs.GetBool(DCLPrefKeys.SETTINGS_HEAD_SYNC_ENABLED);
            Vector3 headYawAndPitch = headIK.GetHeadYawAndPitch();

            playerMovement.LastSentMessage = new NetworkMovementMessage
            {
                timestamp = UnityEngine.Time.unscaledTime,
                position = playerMovement.Character.transform.position,
                velocity = playerMovement.Character.velocity,
                velocitySqrMagnitude = playerMovement.Character.velocity.sqrMagnitude,

                rotationY = playerMovement.Character.transform.eulerAngles.y,

                headIKEnabled = headSyncEnabled && headIK.IsEnabled,
                headYawAndPitch = headYawAndPitch,

                velocityTier = velocityTier,

                isStunned = playerStunComponent.IsStunned,
                isSliding = animation.IsSliding,
                isInstant = isInstant,
                isEmoting = isEmoting,

                animState = new AnimationStates
                {
                    IsGrounded = animation.States.IsGrounded,
                    IsJumping = animation.States.IsJumping,
                    IsLongJump = animation.States.IsLongJump,
                    IsFalling = animation.States.IsFalling,
                    IsLongFall = animation.States.IsLongFall,

                    // Just for testing purposes. We don't send blend values explicitly. It is calculated from MovementKind and IsSliding fields
                    SlideBlendValue = animation.States.SlideBlendValue,
                    MovementBlendValue = animation.States.MovementBlendValue,
                },

                movementKind = input.Kind,
            };

            messageBus.Send(playerMovement.LastSentMessage);

            // Debug purposes. Simulate package lost when Running
            if (debugSettings.SelfSending
                && input.Kind != MovementKind.RUN // simulate package lost when Running
               )
                messageBus.SelfSendWithDelayAsync(playerMovement.LastSentMessage,
                               debugSettings.Latency + (debugSettings.Latency * Random.Range(0, debugSettings.LatencyJitter)))
                          .Forget();
        }

        private byte VelocityTierFromSpeed(float speed)
        {
            byte velocityTier = 0;

            while (velocityTier < settings.VelocityTiers.Length && speed >= settings.VelocityTiers[velocityTier])
                velocityTier++;

            return velocityTier;
        }
    }
}
