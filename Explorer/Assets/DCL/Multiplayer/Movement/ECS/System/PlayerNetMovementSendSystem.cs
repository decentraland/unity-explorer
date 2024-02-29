using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.Character.Components;
using DCL.CharacterMotion.Components;
using DCL.Multiplayer.Connections.Archipelago.Rooms;
using DCL.Multiplayer.Connections.Rooms.Connective;
using DCL.Multiplayer.Movement.MessageBusMock;
using DCL.Multiplayer.Movement.Settings;
using ECS.Abstract;
using System;
using UnityEngine;

namespace DCL.Multiplayer.Movement.ECS.System
{
    [UpdateInGroup(typeof(PostRenderingSystemGroup))]

    // [LogCategory(ReportCategory.AVATAR)]
    public partial class PlayerNetMovementSendSystem : BaseUnityLoopSystem
    {
        private readonly IArchipelagoIslandRoom room;
        private readonly IMultiplayerSpatialStateSettings settings;

        private readonly CharacterController playerCharacter;

        private readonly float maxSentRate = 1.5f;

        // Animations
        private readonly int moveBlendTiersDiff = 2;
        private readonly float minSlideBlendDiff = 0.35f;

        // Velocity
        private readonly float velocityCosAngleChangeThreshold = 0.5f;
        private readonly float velocityChangeThreshold = 5;

        private MessageMock? lastSentMessage;

        public PlayerNetMovementSendSystem(World world, IArchipelagoIslandRoom room, IMultiplayerSpatialStateSettings settings, CharacterController playerCharacter) : base(world)
        {
            this.room = room;
            this.settings = settings;
            this.playerCharacter = playerCharacter;
        }

        protected override void Update(float t)
        {
            SendPlayerNetMovementQuery(World);
        }

        [Query]
        [All(typeof(PlayerComponent))]
        private void SendPlayerNetMovement(ref CharacterAnimationComponent playerAnimationComponent, ref StunComponent playerStunComponent)
        {
            if (room.CurrentState() != IConnectiveRoom.State.Running) return;

            if (lastSentMessage == null)
            {
                SentMessage(ref playerAnimationComponent, ref playerStunComponent, "FIRST");
                return;
            }

            if (UnityEngine.Time.unscaledTime - lastSentMessage?.timestamp < settings.PackageSentRate)
                return;

            // Max sent rate
            if (UnityEngine.Time.unscaledTime - lastSentMessage?.timestamp > maxSentRate)
            {
                SentMessage(ref playerAnimationComponent, ref playerStunComponent, "MAX TIME");
                return;
            }

            // Animation change
            if (AnimationChanged(ref playerAnimationComponent, ref playerStunComponent, minSlideBlendDiff, out string reason))
            {
                SentMessage(ref playerAnimationComponent, ref playerStunComponent, $"<color=olive> ANIM {reason} </color>");
                return;
            }

            // Velocity angle change
            // Dot product: -1 = opposite directions, 0 = perpendicular, 1 = same direction
            if (playerCharacter.velocity != Vector3.zero && lastSentMessage.velocity != Vector3.zero
                                                         && Vector3.Dot(lastSentMessage.velocity.normalized, playerCharacter.velocity) < velocityCosAngleChangeThreshold)
            {
                SentMessage(ref playerAnimationComponent, ref playerStunComponent, "$\"<color=navy> VEL ANGLE {reason} </color>\"");
                return;
            }

            // Velocity diff magnitude change
            if (Vector3.SqrMagnitude(lastSentMessage.velocity - playerCharacter.velocity) > velocityChangeThreshold)
            {
                SentMessage(ref playerAnimationComponent, ref playerStunComponent,  "$\"<color=maroon> VEL DIFF {reason} </color>\"" );
                return;
            }

            // Different min package sent rate for animations and velocity/position

            // // Position diff magnitude change
            // {}

            // { // Alvaro approach - velocity Tiers
            //     // If velocity is high  - send (Alvaro approach)
            //     // Ranged
            //     if (playerCharacter.velocity.sqrMagnitude)
            //     {
            //
            //     }
            // }

            // Projective approach
            Vector3 projectedPosition = lastSentMessage!.position + (lastSentMessage.velocity * (UnityEngine.Time.unscaledTime - lastSentMessage.timestamp));
        }

        private string GetColorBasedOnDeltaTime(float deltaTime)
        {
            return deltaTime switch
                   {
                       > 1.49f => "grey",
                       > 0.99f => "lightblue",
                       > 0.49f => "green",
                       > 0.31f => "yellow",
                       > 0.19f => "orange",
                       _ => "red",
                   };
        }

        private void SentMessage(ref CharacterAnimationComponent playerAnimationComponent, ref StunComponent playerStunComponent, string from)
        {
            float deltaTime = UnityEngine.Time.unscaledTime - (lastSentMessage?.timestamp ?? 0);
            string color = GetColorBasedOnDeltaTime(deltaTime);
            Debug.Log($">VVV {from}: <color={color}> {deltaTime}</color>");

            lastSentMessage = new MessageMock
            {
                timestamp = UnityEngine.Time.unscaledTime,
                position = playerCharacter.transform.position,
                velocity = playerCharacter.velocity,
                animState = playerAnimationComponent.States,
                isStunned = playerStunComponent.IsStunned,
            };

            var byteMessage = new Span<byte>(MessageMockSerializer.SerializeMessage(lastSentMessage));

            // IReadOnlyCollection<string> participants = room.Room().Participants.RemoteParticipantSids();
            room.Room().DataPipe.PublishData(byteMessage, "Movement", null!);
        }

        private bool AnimationChanged(ref CharacterAnimationComponent playerAnimationComponent, ref StunComponent playerStunComponent, float minSlideBlendDiff, out string reason)
        {
            if (lastSentMessage.isStunned != playerStunComponent.IsStunned)
            {
                reason = "STUN";
                return true;
            }

            if (lastSentMessage.animState.IsJumping != playerAnimationComponent.States.IsJumping)
            {
                reason = "JUMP";
                return true;
            }

            if (lastSentMessage.animState.IsGrounded != playerAnimationComponent.States.IsGrounded)
            {
                reason = "GROUND";
                return true;
            }

            if (lastSentMessage.animState.IsFalling != playerAnimationComponent.States.IsFalling)
            {
                reason = "FALL";
                return true;
            }

            if (lastSentMessage.animState.IsLongFall != playerAnimationComponent.States.IsLongFall)
            {
                reason = "LONG FALL";
                return true;
            }

            if (lastSentMessage.animState.IsLongJump != playerAnimationComponent.States.IsLongJump)
            {
                reason = "LONG JUMP";
                return true;
            }

            // Maybe we don't need it because of velocity change?
            if (GetMovementBlendTier(lastSentMessage.animState.MovementBlendValue) - GetMovementBlendTier(playerAnimationComponent.States.MovementBlendValue) >= moveBlendTiersDiff)
            {
                reason = $"MOVEMENT {GetMovementBlendTier(lastSentMessage.animState.MovementBlendValue)} vs {GetMovementBlendTier(playerAnimationComponent.States.MovementBlendValue)}";
                return true;
            }

            if (Mathf.Abs(lastSentMessage.animState.SlideBlendValue - playerAnimationComponent.States.SlideBlendValue) > minSlideBlendDiff)
            {
                reason = "SLIDE";
                return true;
            }

            reason = "";
            return false;
        }

        // state idle ----- walk ----- jog ----- run
        // blend  0  -----   1  -----  2  -----  3
        private static int GetMovementBlendTier(float value) =>
            value switch
            {
                < 1 => 0,
                < 2 => 1,
                < 3 => 2,
                _ => 3,
            };
    }
}
