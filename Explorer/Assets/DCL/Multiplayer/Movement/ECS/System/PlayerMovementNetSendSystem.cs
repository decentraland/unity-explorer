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
using System.Collections.Generic;
using UnityEngine;

namespace DCL.Multiplayer.Movement.ECS.System
{
    [UpdateInGroup(typeof(PostRenderingSystemGroup))]

    // [LogCategory(ReportCategory.AVATAR)]
    public partial class PlayerMovementNetSendSystem : BaseUnityLoopSystem
    {
        private readonly IArchipelagoIslandRoom room;
        private readonly IMultiplayerSpatialStateSettings settings;

        private readonly CharacterController playerCharacter;

        private MessageMock? lastSentMessage;

        private int mesPerSec;
        private float mesPerSecTimer;

        public PlayerMovementNetSendSystem(World world, IArchipelagoIslandRoom room, IMultiplayerSpatialStateSettings settings, CharacterController playerCharacter) : base(world)
        {
            this.room = room;
            this.settings = settings;
            this.playerCharacter = playerCharacter;
        }

        private static string GetColorBasedOnMesPerSec(int amount)
        {
            return amount switch
                   {
                       > 30 => "red",
                       > 20 => "orange",
                       >= 10 => "yellow",
                       >= 5 => "green",
                       >= 3 => "lightblue",
                       >= 2 => "olive",
                       _ => "grey",
                   };
        }

        protected override void Update(float t)
        {
            if (mesPerSecTimer <= 0)
            {
                string color = GetColorBasedOnMesPerSec(mesPerSec);
                Debug.Log($"VVV <color={color}> ------- MES PER SEC: {mesPerSec} ----------</color>");
                mesPerSec = 0;
                mesPerSecTimer = 1;
            }
            else { mesPerSecTimer -= t; }

            SendPlayerNetMovementQuery(World);
        }

        [Query]
        [All(typeof(PlayerComponent))]
        private void SendPlayerNetMovement(ref CharacterAnimationComponent playerAnimationComponent, ref StunComponent playerStunComponent)
        {
            // Debug.Log($"VVV vel = {playerCharacter.velocity.sqrMagnitude}");
            if (room.CurrentState() != IConnectiveRoom.State.Running) return;

            if (lastSentMessage == null)
            {
                SentMessage(ref playerAnimationComponent, ref playerStunComponent, "FIRST");
                return;
            }

            float timeDiff = UnityEngine.Time.unscaledTime - (lastSentMessage?.timestamp ?? 0);

            if (timeDiff >= 1f) mesPerSec = 0;
            if (mesPerSec >= 10) return;

            //----- MAX TIME CHECK -----
            if (timeDiff > settings.MaxSentDelay)
            {
                SentMessage(ref playerAnimationComponent, ref playerStunComponent, "MAX TIME");
                return;
            }

            //----- ANIMATION CHECKS -----
            if (timeDiff > settings.MinAnimPackageTime
                && AnimationChanged(ref playerAnimationComponent, ref playerStunComponent, out string reason))
            {
                SentMessage(ref playerAnimationComponent, ref playerStunComponent, $"<color=olive> ANIM {reason} </color>");
                return;
            }

            //----- VELOCITY AND POSITION CHECKS -----
            if (timeDiff < settings.MinPositionPackageTime)
                return;

            Vector3 extrapolatedVelocity = ExtrapolationComponent.DampVelocity(timeDiff, lastSentMessage, settings);
            Vector3 projectedPosition = lastSentMessage!.position + (extrapolatedVelocity * timeDiff);

            // Proj Velocity diff magnitude change
            if (Vector3.SqrMagnitude(lastSentMessage.velocity - extrapolatedVelocity) > settings.ProjVelocityChangeThreshold)
            {
                SentMessage(ref playerAnimationComponent, ref playerStunComponent, "$\"<color=navy> PROJ VEL DIFF </color>\"");
                return;
            }

            // Proj Position diff magnitude change
            if (Vector3.SqrMagnitude(lastSentMessage.position - projectedPosition) > settings.ProjPositionChangeThreshold)
            {
                SentMessage(ref playerAnimationComponent, ref playerStunComponent, "$\"<color=navy> PROJ POS DIFF </color>\"");
                return;
            }

            // Velocity diff magnitude change
            if (Vector3.SqrMagnitude(lastSentMessage.velocity - playerCharacter.velocity) > settings.VelocityChangeThreshold)
            {
                SentMessage(ref playerAnimationComponent, ref playerStunComponent, "$\"<color=maroon> VEL DIFF </color>\"");
                return;
            }

            // Position diff magnitude change
            if (Vector3.SqrMagnitude(lastSentMessage.position - playerCharacter.transform.position) > settings.PositionChangeThreshold)
            {
                SentMessage(ref playerAnimationComponent, ref playerStunComponent, "$\"<color=maroon> POS DIFF </color>\"");
                return;
            }

            // Velocity angle change (Dot product): -1 = opposite directions, 0 = perpendicular, 1 = same direction
            if (lastSentMessage.velocity != Vector3.zero && playerCharacter.velocity != Vector3.zero &&
                Vector3.Dot(lastSentMessage.velocity.normalized, playerCharacter.velocity.normalized) < settings.VelocityCosAngleChangeThreshold)
            {
                SentMessage(ref playerAnimationComponent, ref playerStunComponent, "$\"<color=maroon> VEL ANGLE </color>\"");
                return;
            }

            // Velocity tiers - 0 = idle, 1 = walk, 2 = run, 3 = sprint
            if (playerCharacter.velocity.sqrMagnitude > settings.SprintSqrSpeed && timeDiff > settings.SprintSentRate)
            {
                SentMessage(ref playerAnimationComponent, ref playerStunComponent, "$\"<color=maroon> VEL TIERS SPRINT </color>\"");
                return;
            }
            if (playerCharacter.velocity.sqrMagnitude > settings.RunSqrSpeed && timeDiff > settings.RunSentRate)
            {
                SentMessage(ref playerAnimationComponent, ref playerStunComponent, "$\"<color=maroon> VEL TIERS RUN </color>\"");
                return;
            }
            if (playerCharacter.velocity.sqrMagnitude > settings.WalkSqrSpeed && timeDiff > settings.WalkSentRate)
            {
                SentMessage(ref playerAnimationComponent, ref playerStunComponent, "$\"<color=maroon> VEL TIERS WALK </color>\"");
                return;
            }
        }

        private static string GetColorBasedOnDeltaTime(float deltaTime)
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
            mesPerSec++;

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

            IReadOnlyCollection<string>? participants = room is IslandRoomMock ? null : room.Room().Participants.RemoteParticipantSids();
            room.Room().DataPipe.PublishData(byteMessage, "Movement", participants!);
        }

        private bool AnimationChanged(ref CharacterAnimationComponent playerAnimationComponent, ref StunComponent playerStunComponent, out string reason)
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
            if (GetMovementBlendTier(lastSentMessage.animState.MovementBlendValue) - GetMovementBlendTier(playerAnimationComponent.States.MovementBlendValue) >= settings.MoveBlendTiersDiff)
            {
                reason = $"MOVEMENT {GetMovementBlendTier(lastSentMessage.animState.MovementBlendValue)} vs {GetMovementBlendTier(playerAnimationComponent.States.MovementBlendValue)}";
                return true;
            }

            if (Mathf.Abs(lastSentMessage.animState.SlideBlendValue - playerAnimationComponent.States.SlideBlendValue) > settings.MinSlideBlendDiff)
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
