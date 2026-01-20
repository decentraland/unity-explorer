using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.Character.CharacterMotion.Components;
using DCL.Character.CharacterMotion.Velocity;
using DCL.CharacterCamera;
using DCL.CharacterMotion.Components;
using DCL.CharacterMotion.Settings;
using DCL.DebugUtilities;
using DCL.DebugUtilities.UIBindings;
using DCL.Time.Systems;
using ECS.Abstract;
using ECS.LifeCycle.Components;
using UnityEngine;
using DCL.AvatarRendering.DemoScripts.Components;
using DCL.CharacterMotion.Utils;

namespace DCL.CharacterMotion.Systems
{
    /// <summary>
    ///     Entry point to calculate everything that affects character's velocity
    /// </summary>
    [UpdateInGroup(typeof(PhysicsSystemGroup))]
    [UpdateAfter(typeof(UpdatePhysicsTickSystem))]
    public partial class CalculateCharacterVelocitySystem : BaseUnityLoopSystem
    {
        private SingleInstanceEntity camera;
        private SingleInstanceEntity fixedTick;
        private SingleInstanceEntity entitySettings;

        private readonly ElementBinding<float> cameraRunFov = new (0);
        private readonly ElementBinding<float> walkSpeed = new (0);
        private readonly ElementBinding<float> jogSpeed = new (0);
        private readonly ElementBinding<float> runSpeed = new (0);
        private readonly ElementBinding<float> jogJumpHeight = new (0);
        private readonly ElementBinding<float> runJumpHeight = new (0);
        private readonly ElementBinding<float> jumpHold = new (0);
        private readonly ElementBinding<float> jumpHoldGravity = new (0);
        private readonly ElementBinding<float> gravity = new (0);
        private readonly ElementBinding<float> airAcc = new (0);
        private readonly ElementBinding<float> maxAirAcc = new (0);
        private readonly ElementBinding<float> airDrag = new (0);
        private readonly ElementBinding<float> stopTime = new (0);

        private readonly ElementBinding<int> airJumpCount = new (0);
        private readonly ElementBinding<float> airJumpHeight = new (0);
        private readonly ElementBinding<float> cooldownBetweenJumps = new (0);
        private readonly ElementBinding<float> airJumpImpulse = new (0);

        public CalculateCharacterVelocitySystem(World world, IDebugContainerBuilder debugBuilder) : base(world)
        {
            debugBuilder.TryAddWidget("Locomotion: Base")?
                        .AddFloatField("Camera Run FOV", cameraRunFov)
                        .AddFloatField("Walk Speed", walkSpeed)
                        .AddFloatField("Jog Speed", jogSpeed)
                        .AddFloatField("Run Speed", runSpeed)
                        .AddFloatField("Jog Jump Height", jogJumpHeight)
                        .AddFloatField("Run Jump Height", runJumpHeight)
                        .AddFloatField("Jump Hold Time", jumpHold)
                        .AddFloatField("Jump Hold Gravity Scale", jumpHoldGravity)
                        .AddFloatField("Gravity", gravity)
                        .AddFloatField("Air Acceleration", airAcc)
                        .AddFloatField("Max Air Acceleration", maxAirAcc)
                        .AddFloatField("Air Drag", airDrag)
                        .AddFloatField("Grounded Stop Time", stopTime);

            debugBuilder.TryAddWidget("Locomotion: Air Jumping")?
                        .AddControl( new DebugConstLabelDef("Air Jump Count"), new DebugIntFieldDef(airJumpCount) )
                        .AddFloatField("Height", airJumpHeight)
                        .AddFloatField("Cooldown", cooldownBetweenJumps)
                        .AddFloatField("Direction Change Impulse", airJumpImpulse);
        }

        public override void Initialize()
        {
            camera = World.CacheCamera();
            fixedTick = World.CachePhysicsTick();
            entitySettings = World.CacheCharacterSettings();

            ICharacterControllerSettings settings = entitySettings.GetCharacterSettings(World);
            cameraRunFov.Value = settings.CameraFOVWhileRunning;
            walkSpeed.Value = settings.WalkSpeed;
            jogSpeed.Value = settings.JogSpeed;
            runSpeed.Value = settings.RunSpeed;
            jogJumpHeight.Value = settings.JogJumpHeight;
            runJumpHeight.Value = settings.RunJumpHeight;
            jumpHold.Value = settings.LongJumpTime;
            jumpHoldGravity.Value = settings.LongJumpGravityScale;
            gravity.Value = settings.Gravity;
            airAcc.Value = settings.AirAcceleration;
            maxAirAcc.Value = settings.MaxAirAcceleration;
            airDrag.Value = settings.AirDrag;
            stopTime.Value = settings.StopTimeSec;
            airJumpCount.Value = settings.AirJumpCount;
            airJumpHeight.Value = settings.AirJumpHeight;
            cooldownBetweenJumps.Value = settings.CooldownBetweenJumps;
            airJumpImpulse.Value = settings.AirJumpDirectionChangeImpulse;
        }

        protected override void Update(float t)
        {
            ICharacterControllerSettings settings = entitySettings.GetCharacterSettings(World);

            settings.CameraFOVWhileRunning = cameraRunFov.Value;
            settings.WalkSpeed = walkSpeed.Value;
            settings.JogSpeed = jogSpeed.Value;
            settings.RunSpeed = runSpeed.Value;
            settings.JogJumpHeight = jogJumpHeight.Value;
            settings.RunJumpHeight = runJumpHeight.Value;
            settings.LongJumpTime = jumpHold.Value;
            settings.LongJumpGravityScale = jumpHoldGravity.Value;
            settings.Gravity = gravity.Value;
            settings.AirAcceleration = airAcc.Value;
            settings.MaxAirAcceleration = maxAirAcc.Value;
            settings.AirDrag = airDrag.Value;
            settings.StopTimeSec = stopTime.Value;
            settings.AirJumpCount = airJumpCount.Value;
            settings.AirJumpHeight = airJumpHeight.Value;
            settings.CooldownBetweenJumps = cooldownBetweenJumps.Value;
            settings.AirJumpDirectionChangeImpulse = airJumpImpulse.Value;

            ResolveVelocityQuery(World, t, fixedTick.GetPhysicsTickComponent(World).Tick, in camera.GetCameraComponent(World));
            ResolveRandomAvatarVelocityQuery(World, t, fixedTick.GetPhysicsTickComponent(World).Tick, in camera.GetCameraComponent(World));
        }

        [Query]
        [None(typeof(DeleteEntityIntention), typeof(RandomAvatar))]
        private void ResolveVelocity(
            [Data] float dt,
            [Data] int physicsTick,
            [Data] in CameraComponent cameraComponent,
            ref ICharacterControllerSettings settings,
            ref CharacterRigidTransform rigidTransform,
            ref CharacterController characterController,
            ref JumpInputComponent jump,
            in MovementInputComponent movementInput,
            ref GlideState glideState,
            in MovementSpeedLimit speedLimit)
        {
            ResolveAvatarVelocity(dt,
                physicsTick,
                in cameraComponent,
                ref settings,
                ref rigidTransform,
                ref characterController,
                ref jump,
                ref glideState,
                in movementInput,
                in speedLimit,
                cameraComponent.Camera.transform);
        }

        [Query]
        [All(typeof(RandomAvatar))]
        [None(typeof(DeleteEntityIntention))]
        private void ResolveRandomAvatarVelocity(
            [Data] float dt,
            [Data] int physicsTick,
            [Data] in CameraComponent cameraComponent,
            ref ICharacterControllerSettings settings,
            ref CharacterRigidTransform rigidTransform,
            ref CharacterController characterController,
            ref JumpInputComponent jump,
            in MovementInputComponent movementInput,
            ref GlideState glideState,
            in MovementSpeedLimit speedLimit)
        {
            // Random avatars are not affected by the player's camera
            ResolveAvatarVelocity(dt,
                physicsTick,
                in cameraComponent,
                ref settings,
                ref rigidTransform,
                ref characterController,
                ref jump,
                ref glideState,
                in movementInput,
                in speedLimit,
                characterController.transform);
        }

        private void ResolveAvatarVelocity(
            [Data] float dt,
            [Data] int physicsTick,
            [Data] in CameraComponent cameraComponent,
            ref ICharacterControllerSettings settings,
            ref CharacterRigidTransform rigidTransform,
            ref CharacterController characterController,
            ref JumpInputComponent jump,
            ref GlideState glideState,
            in MovementInputComponent movementInput,
            in MovementSpeedLimit speedLimit,
            Transform viewerTransform)
        {
            var viewerForward = LookDirectionUtils.FlattenLookDirection(viewerTransform.forward, viewerTransform.up);
            var viewerRight = Vector3.Cross(-viewerForward, Vector3.up);

            // Apply velocity based on input
            ApplyCharacterMovementVelocity.Execute(settings, ref rigidTransform, viewerForward, viewerRight, in movementInput, speedLimit.Value, dt);

            // Apply velocity based on edge slip
            ApplyEdgeSlip.Execute(dt, settings, ref rigidTransform, characterController);

            // Apply velocity multiplier based on walls
            ApplyWallSlide.Execute(ref rigidTransform, characterController, in settings);

            // Apply vertical velocity
            ApplyJump.Execute(settings, ref rigidTransform, ref jump, viewerForward, viewerRight, in movementInput, physicsTick);
            ApplyGravity.Execute(settings, ref rigidTransform, in jump, physicsTick, dt);
            ApplyGliding.Execute(settings, in rigidTransform, in jump, ref glideState, physicsTick);

            ApplyAirDrag.Execute(settings, ref rigidTransform, dt);

            if (cameraComponent.Mode == CameraMode.FirstPerson)
                ApplyFirstPersonRotation.Execute(ref rigidTransform, in cameraComponent);
            else
                ApplyThirdPersonRotation.Execute(ref rigidTransform, in movementInput);

            ApplyConditionalRotation.Execute(ref rigidTransform, in settings);
        }
    }
}
