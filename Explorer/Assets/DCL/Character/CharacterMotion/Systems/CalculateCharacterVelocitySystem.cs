using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.Character.CharacterMotion.Components;
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

        private readonly ElementBinding<float> cameraRunFov;
        private readonly ElementBinding<float> cameraFovSpeed;
        private readonly ElementBinding<float> walkSpeed;
        private readonly ElementBinding<float> jogSpeed;
        private readonly ElementBinding<float> runSpeed;
        private readonly ElementBinding<float> jogJumpHeight;
        private readonly ElementBinding<float> runJumpHeight;
        private readonly ElementBinding<float> jumpHold;
        private readonly ElementBinding<float> jumpHoldGravity;
        private readonly ElementBinding<float> gravity;
        private readonly ElementBinding<float> airAcc;
        private readonly ElementBinding<float> maxAirAcc;
        private readonly ElementBinding<float> airDrag;
        private readonly ElementBinding<float> stopTime;

        public CalculateCharacterVelocitySystem(World world, IDebugContainerBuilder debugBuilder) : base(world)
        {
            debugBuilder.AddWidget("Locomotion: Base")
                        .AddFloatField("Camera Run FOV", cameraRunFov = new ElementBinding<float>(0))
                        .AddFloatField("Camera FOV Speed", cameraFovSpeed = new ElementBinding<float>(0))
                        .AddFloatField("Walk Speed", walkSpeed = new ElementBinding<float>(0))
                        .AddFloatField("Jog Speed", jogSpeed = new ElementBinding<float>(0))
                        .AddFloatField("Run Speed", runSpeed = new ElementBinding<float>(0))
                        .AddFloatField("Jog Jump Height", jogJumpHeight = new ElementBinding<float>(0))
                        .AddFloatField("Run Jump Height", runJumpHeight = new ElementBinding<float>(0))
                        .AddFloatField("Jump Hold Time", jumpHold = new ElementBinding<float>(0))
                        .AddFloatField("Jump Hold Gravity Scale", jumpHoldGravity = new ElementBinding<float>(0))
                        .AddFloatField("Gravity", gravity = new ElementBinding<float>(0))
                        .AddFloatField("Air Acceleration", airAcc = new ElementBinding<float>(0))
                        .AddFloatField("Max Air Acceleration", maxAirAcc = new ElementBinding<float>(0))
                        .AddFloatField("Air Drag", airDrag = new ElementBinding<float>(0))
                        .AddFloatField("Grounded Stop Time", stopTime = new ElementBinding<float>(0))
                ;
        }

        public override void Initialize()
        {
            camera = World.CacheCamera();
            fixedTick = World.CachePhysicsTick();
            entitySettings = World.CacheCharacterSettings();

            ICharacterControllerSettings settings = entitySettings.GetCharacterSettings(World);
            cameraRunFov.Value = settings.CameraFOVWhileRunning;
            cameraFovSpeed.Value = settings.FOVChangeSpeed;
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
            settings.FOVChangeSpeed = cameraFovSpeed.Value;
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

            // Update look direction based on the final velocity
            ApplyLookDirection.Execute(rigidTransform, in movementInput, in settings);
        }
    }
}
