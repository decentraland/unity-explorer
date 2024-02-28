using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.AvatarRendering.AvatarShape.UnityInterface;
using DCL.AvatarRendering.DemoScripts.Systems;
using DCL.CharacterMotion.Animation;
using DCL.CharacterMotion.Components;
using DCL.DebugUtilities;
using DCL.DebugUtilities.UIBindings;
using DCL.Diagnostics;
using DCL.Multiplayer.Movement.MessageBusMock;
using ECS.Abstract;
using System;
using UnityEngine;
using IDebugContainerBuilder = DCL.DebugUtilities.IDebugContainerBuilder;

namespace DCL.Multiplayer.Movement.ECS.System
{
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateAfter(typeof(InstantiateRandomAvatarsSystem))]
    [LogCategory(ReportCategory.AVATAR)]
    public partial class ReceiverMockSystem : BaseUnityLoopSystem
    {
        private readonly MessagePipeMock pipe;
        private readonly DebugWidgetVisibilityBinding debugVisibilityBinding;

        private readonly ElementBinding<string> inboxMessages;
        private readonly ElementBinding<string> passedMessages;

        private readonly ElementBinding<float> packageSentRate;
        private readonly ElementBinding<float> packageJitter;
        private readonly ElementBinding<float> latency;
        private readonly ElementBinding<float> latencyJitter;

        // Teleportation
        private readonly ElementBinding<float> telMinPositionDelta;
        private readonly ElementBinding<float> telMinTeleportDistance;

        // EXTRAPOLATION
        private readonly ElementBinding<bool> useExtrapolation;
        private readonly ElementBinding<float> extMinSpeed;
        private readonly ElementBinding<float> extLinearTime;
        private readonly ElementBinding<int> extDampedSteps;

        // Interpolation
        private readonly ElementBinding<int> intSpeedUpFactor;
        private readonly ElementBinding<bool> useBlend;
        private readonly ElementBinding<float> blendMaxSpeed;

        private readonly ElementBinding<string> status;

        private ReceiverMockSystem(World world, IDebugContainerBuilder debugBuilder, MessagePipeMock pipe) : base(world)
        {
            this.pipe = pipe;

            status = new ElementBinding<string>(string.Empty);

            inboxMessages = new ElementBinding<string>(this.pipe.Settings.InboxCount.ToString());
            passedMessages = new ElementBinding<string>(this.pipe.Settings.PassedMessages.ToString());

            packageSentRate = new ElementBinding<float>(pipe.Settings.PackageSentRate);
            packageJitter = new ElementBinding<float>(pipe.Settings.PackagesJitter);
            latency = new ElementBinding<float>(pipe.Settings.Latency);
            latencyJitter = new ElementBinding<float>(pipe.Settings.LatencyJitter);

            // Teleportation
            telMinPositionDelta = new ElementBinding<float>(pipe.Settings.MinPositionDelta);
            telMinTeleportDistance = new ElementBinding<float>(pipe.Settings.MinTeleportDistance);

            // EXTRAPOLATION
            useExtrapolation = new ElementBinding<bool>(pipe.Settings.useExtrapolation);
            extMinSpeed = new ElementBinding<float>(pipe.Settings.MinSpeed);
            extLinearTime = new ElementBinding<float>(pipe.Settings.LinearTime);
            extDampedSteps = new ElementBinding<int>(pipe.Settings.DampedSteps);

            // Interpolation
            ElementBinding<string> intTypesBinding = new (pipe.Settings.InterpolationType.ToString(),
                evt => pipe.Settings.InterpolationType = (InterpolationType)Enum.Parse(typeof(InterpolationType), evt.newValue));

            intSpeedUpFactor = new ElementBinding<int>((int)pipe.Settings.SpeedUpFactor);

            useBlend = new ElementBinding<bool>(pipe.Settings.useBlend);

            ElementBinding<string> blendTypesBinding = new (pipe.Settings.BlendType.ToString(),
                evt => pipe.Settings.BlendType = (InterpolationType)Enum.Parse(typeof(InterpolationType), evt.newValue));

            blendMaxSpeed = new ElementBinding<float>(pipe.Settings.MaxBlendSpeed);

            debugBuilder.AddWidget("MULTIPLAYER MOVEMENT")
                        .SetVisibilityBinding(debugVisibilityBinding = new DebugWidgetVisibilityBinding(true))

                         // NETWORK
                        .AddControl(new DebugSetOnlyLabelDef(status), null)
                        .AddControl(new DebugHintDef("X - block sending, M - one package lost"), null)
                        .AddSingleButton("Toggle Network", () => pipe.Settings.StartSending = !pipe.Settings.StartSending)
                        .AddCustomMarker("Inbox Messages: ", inboxMessages)
                        .AddCustomMarker("Passed Messages: ", passedMessages)
                        .AddFloatField("Package Sent Rate", packageSentRate)
                        .AddFloatField("Package Jitter", packageJitter)
                        .AddFloatField("Latency", latency)
                        .AddFloatField("Latency Jitter", latencyJitter)

                         // TELEPORTATION
                        .AddControl(new DebugHintDef("TELEPORTATION"), null)
                        .AddFloatField("Min Position Delta", telMinPositionDelta)
                        .AddFloatField("Min Teleport Distance", telMinTeleportDistance)

                         // INTERPOLATION
                        .AddControl(new DebugHintDef("INTERPOLATION"), null)
                        .AddControl(new DebugDropdownDef(Interpolate.Types, intTypesBinding, "Int. Type"), null)
                        .AddControl(new DebugConstLabelDef("Int. SpeedUp Factor"), new DebugIntFieldDef(intSpeedUpFactor))
                        .AddToggleField("Use Blend", evt => useBlend.Value = !useBlend.Value, useBlend.Value)
                        .AddControl(new DebugDropdownDef(Interpolate.Types, blendTypesBinding, "Blend Type"), null)
                        .AddFloatField("Max Blend Speed", blendMaxSpeed)

                         // EXTRAPOLATION
                        .AddControl(new DebugHintDef("EXTRAPOLATION"), null)
                        .AddToggleField("Use Extrapolation", evt => useExtrapolation.Value = !useExtrapolation.Value, useExtrapolation.Value)
                        .AddFloatField("Ext. Min Speed", extMinSpeed)
                        .AddFloatField("Ext. Linear Time", extLinearTime)
                        .AddControl(new DebugConstLabelDef("Ext. Damping Steps"), new DebugIntFieldDef(extDampedSteps))
                ;
        }

        protected override void Update(float t)
        {
            status.Value = pipe.Settings.StartSending ? "<color=green>ON</color>" : "<color=red>OFF</color>";

            pipe.Settings.InboxCount = pipe.Count;
            inboxMessages.Value = pipe.Count.ToString();

            pipe.Settings.PackageSentRate = packageSentRate.Value;
            pipe.Settings.PackagesJitter = packageJitter.Value;
            pipe.Settings.Latency = latency.Value;
            pipe.Settings.LatencyJitter = latencyJitter.Value;

            // Teleportation
            pipe.Settings.MinPositionDelta = telMinPositionDelta.Value;
            pipe.Settings.MinTeleportDistance = telMinTeleportDistance.Value;

            // EXTRAPOLATION
            pipe.Settings.useExtrapolation = useExtrapolation.Value;
            pipe.Settings.MinSpeed = extMinSpeed.Value;
            pipe.Settings.LinearTime = extLinearTime.Value;
            pipe.Settings.DampedSteps = extDampedSteps.Value;

            // Interpolation
            // public InterpolationType InterpolationType;
            pipe.Settings.SpeedUpFactor = intSpeedUpFactor.Value;
            pipe.Settings.useBlend = useBlend.Value;

            // public InterpolationType BlendType;
            pipe.Settings.MaxBlendSpeed = blendMaxSpeed.Value;

            UpdateInterpolationQuery(World);
        }

        [Query]
        private void UpdateInterpolation(ref ReplicaMovementComponent replicaMovement, ref InterpolationComponent @int, ref ExtrapolationComponent ext,
            ref CharacterAnimationComponent anim, in IAvatarView view)
        {
            status.Value += ext.Enabled ? " | <color=green>EXT</color>" : " | <color=red>EXT</color>";
            status.Value += @int is { Enabled: true, IsBlend: false }? " | <color=green>INT</color>" : " | <color=red>INT</color>";
            status.Value += @int is { Enabled: true, IsBlend: true } ? " | <color=green>BLEND</color>" : " | <color=red>BLEND</color>";

            pipe.Settings.PassedMessages = replicaMovement.PassedMessages.Count;
            passedMessages.Value = replicaMovement.PassedMessages.Count.ToString();

            if (@int.Enabled)
            {
                MessageMock? passed = @int.Update(UnityEngine.Time.deltaTime);

                if (passed != null)
                    AddToPassed(passed, ref replicaMovement, ref anim, view);

                return;
            }

            if (pipe.Count == 0 && replicaMovement.PassedMessages.Count > 1 && pipe.Settings.useExtrapolation)
            {
                if (!ext.Enabled)
                    ext.Run(replicaMovement.PassedMessages[^1], pipe.Settings);

                ext.Update(UnityEngine.Time.deltaTime);
                return;
            }

            if (pipe.Count > 0)
            {
                MessageMock remote = pipe.Dequeue();
                MessageMock local = null;

                if (ext.Enabled)
                {
                    if (remote.timestamp < ext.Start.timestamp + ext.Time)
                        return;

                    local = ext.Stop();
                    AddToPassed(local, ref replicaMovement, ref anim, view);
                }

                if (replicaMovement.PassedMessages.Count == 0
                    || Vector3.Distance(replicaMovement.PassedMessages[^1].position, remote.position) < pipe.Settings.MinPositionDelta
                    || Vector3.Distance(replicaMovement.PassedMessages[^1].position, remote.position) > pipe.Settings.MinTeleportDistance)
                {
                    // Teleport
                    @int.Transform.position = remote.position;
                    replicaMovement.PassedMessages.Clear();
                    AddToPassed(remote, ref replicaMovement, ref anim, view);
                }
                else
                    @int.Run(replicaMovement.PassedMessages[^1], remote, pipe.Count, pipe.Settings, local != null && pipe.Settings.useBlend);
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
