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
using DCL.Input;
using DCL.Time.Systems;
using ECS.Abstract;
using UnityEngine;

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
        private readonly ElementBinding<float> cameraFovSpeed = new (0);
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

        public CalculateCharacterVelocitySystem(World world, IDebugContainerBuilder debugBuilder) : base(world)
        {
            debugBuilder.TryAddWidget("Locomotion: Base")
                       ?.AddFloatField("Camera Run FOV", cameraRunFov)
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
                        .AddFloatField("Grounded Stop Time", stopTime)
                ;
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

            ResolveVelocityQuery(World, t, fixedTick.GetPhysicsTickComponent(World).Tick, in camera.GetCameraComponent(World));
        }

        [Query]
        private void ResolveVelocity(
            [Data] float dt,
            [Data] int physicsTick,
            [Data] in CameraComponent cameraComponent,
            ref ICharacterControllerSettings settings,
            ref CharacterRigidTransform rigidTransform,
            ref CharacterController characterController,
            ref JumpInputComponent jump,
            in MovementInputComponent movementInput)
        {
            // Apply velocity based on input
            ApplyCharacterMovementVelocity.Execute(settings, ref rigidTransform, in cameraComponent, in movementInput, dt);

            // Apply velocity based on edge slip
            ApplyEdgeSlip.Execute(dt, settings, ref rigidTransform, characterController);

            // Apply velocity multiplier based on walls
            ApplyWallSlide.Execute(ref rigidTransform, characterController, in settings);

            // Apply vertical velocity
            ApplyJump.Execute(settings, ref rigidTransform, ref jump, in movementInput, physicsTick);
            ApplyGravity.Execute(settings, ref rigidTransform, in jump, physicsTick, dt);
            ApplyAirDrag.Execute(settings, ref rigidTransform, dt);

            if (cameraComponent.Mode == CameraMode.FirstPerson)
                ApplyFirstPersonRotation.Execute(ref rigidTransform, in cameraComponent);
            else
                ApplyThirdPersonRotation.Execute(ref rigidTransform, in movementInput);

            ApplyConditionalRotation.Execute(ref rigidTransform, in settings);
        }
    }
}
