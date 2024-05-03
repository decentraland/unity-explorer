using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using DCL.Character.CharacterMotion.Components;
using DCL.AvatarRendering.AvatarShape.UnityInterface;
using DCL.CharacterMotion.Components;
using DCL.Diagnostics;
using DCL.Multiplayer.Movement.Settings;
using ECS.Abstract;
using Random = UnityEngine.Random;

namespace DCL.Multiplayer.Movement.Systems
{
    [UpdateInGroup(typeof(RemoteMotionGroup))]
    [LogCategory(ReportCategory.MULTIPLAYER_MOVEMENT)]
    public partial class PlayerMovementNetSendSystem : BaseUnityLoopSystem
    {
        private readonly MultiplayerMovementMessageBus messageBus;
        private readonly IMultiplayerMovementSettings settings;

        public PlayerMovementNetSendSystem(World world, MultiplayerMovementMessageBus messageBus, IMultiplayerMovementSettings settings) : base(world)
        {
            this.messageBus = messageBus;
            this.settings = settings;
        }

        protected override void Update(float t)
        {
            SendPlayerNetMovementQuery(World, t);
        }

        [Query]
        private void SendPlayerNetMovement([Data] float t, ref PlayerMovementNetworkComponent playerMovement, in IAvatarView view, ref CharacterAnimationComponent anim, ref StunComponent stun,
            ref MovementInputComponent move,
            ref JumpInputComponent jump)
        {
            UpdateMessagePerSecondTimer(t, ref playerMovement);

            if (playerMovement.MessagesSentInSec >= PlayerMovementNetworkComponent.MAX_MESSAGES_PER_SEC) return;

            if (playerMovement.IsFirstMessage)
            {
                SendMessage(ref playerMovement, view, in anim, in stun, in move);
                playerMovement.IsFirstMessage = false;
                return;
            }

            float timeDiff = UnityEngine.Time.unscaledTime - playerMovement.LastSentMessage.timestamp;

            foreach (SendRuleBase sendRule in settings.SendRules)
                if (timeDiff > sendRule.MinTimeDelta
                    && sendRule.IsSendConditionMet(timeDiff, in playerMovement.LastSentMessage, in anim, in stun, in move, in jump, playerMovement.Character, settings))
                {
                    SendMessage(ref playerMovement, view, in anim, in stun, in move);
                    return;
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

        private void SendMessage(ref PlayerMovementNetworkComponent playerMovement, in IAvatarView view, in CharacterAnimationComponent animation, in StunComponent playerStunComponent, in MovementInputComponent movement)
        {
            playerMovement.MessagesSentInSec++;

            playerMovement.LastSentMessage = new NetworkMovementMessage
            {
                timestamp = UnityEngine.Time.unscaledTime,
                position = playerMovement.Character.transform.position,
                velocity = playerMovement.Character.velocity,
                animState = animation.States,
                isStunned = playerStunComponent.IsStunned,
            };

            // We use AnimatorController value directly, because AnimationState is not always equal to actual Controller due to the blend shapes. Check ApplyAnimationMovementBlend.cs logic for more details.
            // TODO (Vit): refactor to use velocity to calculate the blend value
            playerMovement.LastSentMessage.animState.MovementBlendValue = view.GetAnimatorFloat(AnimationHashes.MOVEMENT_BLEND);

            messageBus.Send(playerMovement.LastSentMessage);

            // Debug purposes. Simulate package lost when Running
            if (settings.SelfSending && movement.Kind != MovementKind.Run)
                messageBus.SelfSendWithDelayAsync(playerMovement.LastSentMessage, settings.Latency + (settings.Latency * Random.Range(0, settings.LatencyJitter))).Forget();
        }
    }
}
