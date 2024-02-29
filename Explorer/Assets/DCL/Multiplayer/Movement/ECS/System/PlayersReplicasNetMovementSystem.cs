using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using Cysharp.Threading.Tasks;
using DCL.AvatarRendering.AvatarShape.UnityInterface;
using DCL.AvatarRendering.DemoScripts.Systems;
using DCL.Character.Components;
using DCL.CharacterMotion.Animation;
using DCL.CharacterMotion.Components;
using DCL.Multiplayer.Connections.Archipelago.Rooms;
using DCL.Multiplayer.Movement.MessageBusMock;
using DCL.Multiplayer.Movement.Settings;
using ECS.Abstract;
using UnityEngine;

namespace DCL.Multiplayer.Movement.ECS.System
{
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateAfter(typeof(InstantiateRandomAvatarsSystem))]
    // [LogCategory(ReportCategory.AVATAR)]
    public partial class PlayersReplicasNetMovementSystem : BaseUnityLoopSystem
    {
        private readonly IMultiplayerSpatialStateSettings settings;
        private readonly ReplicasMovementInbox inbox;

        public PlayersReplicasNetMovementSystem(World world, IArchipelagoIslandRoom room, IMultiplayerSpatialStateSettings settings) : base(world)
        {
            this.settings = settings;
            inbox = new ReplicasMovementInbox(room, settings);

            inbox.InitializeAsync().Forget();
        }

        protected override void Update(float t)
        {
            settings.InboxCount = inbox.Count;

            UpdateReplicasMovementQuery(World);
        }

        [Query]
        [None(typeof(PlayerComponent))]
        private void UpdateReplicasMovement(ref ReplicaMovementComponent replicaMovement, ref InterpolationComponent @int, ref ExtrapolationComponent ext,
            ref CharacterAnimationComponent anim, in IAvatarView view)
        {
            settings.PassedMessages = replicaMovement.PassedMessages.Count;

            if (@int.Enabled)
            {
                MessageMock? passed = @int.Update(UnityEngine.Time.deltaTime);

                if (passed != null)
                    AddToPassed(passed, ref replicaMovement, ref anim, view);

                return;
            }

            if (inbox.Count == 0 && replicaMovement.PassedMessages.Count > 1 && settings.useExtrapolation)
            {
                if (!ext.Enabled)
                    ext.Run(replicaMovement.PassedMessages[^1], settings);

                ext.Update(UnityEngine.Time.deltaTime);
                return;
            }

            if (inbox.Count > 0)
            {
                MessageMock remote = inbox.Dequeue();
                MessageMock local = null;

                if (ext.Enabled)
                {
                    if (remote.timestamp < ext.Start.timestamp + ext.Time)
                        return;

                    local = ext.Stop();
                    AddToPassed(local, ref replicaMovement, ref anim, view);
                }

                if (replicaMovement.PassedMessages.Count == 0
                    || Vector3.Distance(replicaMovement.PassedMessages[^1].position, remote.position) < settings.MinPositionDelta
                    || Vector3.Distance(replicaMovement.PassedMessages[^1].position, remote.position) > settings.MinTeleportDistance)
                {
                    // Teleport
                    @int.Transform.position = remote.position;
                    replicaMovement.PassedMessages.Clear();
                    AddToPassed(remote, ref replicaMovement, ref anim, view);
                }
                else
                    @int.Run(replicaMovement.PassedMessages[^1], remote, inbox.Count, settings, local != null && settings.useBlend);
            }
        }

        private static void AddToPassed(MessageMock passed, ref ReplicaMovementComponent replicaMovement, ref CharacterAnimationComponent anim, in IAvatarView view)
        {
            replicaMovement.PassedMessages.Add(passed);
            UpdateAnimations(passed, ref anim, view);
        }

        private static void UpdateAnimations(MessageMock message, ref CharacterAnimationComponent animationComponent, IAvatarView view)
        {
            animationComponent.States = message.animState;

            // TODO: Interpolate between blending states!
            view.SetAnimatorFloat(AnimationHashes.MOVEMENT_BLEND, animationComponent.States.MovementBlendValue);
            view.SetAnimatorFloat(AnimationHashes.SLIDE_BLEND, animationComponent.States.SlideBlendValue);

            if (view.GetAnimatorBool(AnimationHashes.JUMPING))
                view.SetAnimatorTrigger(AnimationHashes.JUMP);

            view.SetAnimatorBool(AnimationHashes.STUNNED, message.isStunned);
            view.SetAnimatorBool(AnimationHashes.GROUNDED, animationComponent.States.IsGrounded);
            view.SetAnimatorBool(AnimationHashes.JUMPING, animationComponent.States.IsJumping);
            view.SetAnimatorBool(AnimationHashes.FALLING, animationComponent.States.IsFalling);
            view.SetAnimatorBool(AnimationHashes.LONG_JUMP, animationComponent.States.IsLongJump);
            view.SetAnimatorBool(AnimationHashes.LONG_FALL, animationComponent.States.IsLongFall);
        }
    }
}
