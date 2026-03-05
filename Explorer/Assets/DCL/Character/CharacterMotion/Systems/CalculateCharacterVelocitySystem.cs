using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.AvatarRendering.DemoScripts.Components;
using DCL.Character.CharacterMotion.Components;
using DCL.Character.CharacterMotion.Velocity;
using DCL.CharacterCamera;
using DCL.CharacterMotion;
using DCL.CharacterMotion.Components;
using DCL.CharacterMotion.Settings;
using DCL.Time.Systems;
using ECS.Abstract;
using ECS.LifeCycle.Components;
using UnityEngine;
using DCL.AvatarRendering.DemoScripts.Components;
using DCL.CharacterMotion.Utils;

namespace DCL.Character.CharacterMotion.Systems
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

        public CalculateCharacterVelocitySystem(World world) : base(world) { }

        public override void Initialize()
        {
            camera = World.CacheCamera();
            fixedTick = World.CachePhysicsTick();
        }

        protected override void Update(float t)
        {
            ResolveVelocityQuery(World, t, fixedTick.GetPhysicsTickComponent(World).Tick, in camera.GetCameraComponent(World));
            ResolveRandomAvatarVelocityQuery(World, t, fixedTick.GetPhysicsTickComponent(World).Tick, in camera.GetCameraComponent(World));
        }

        [Query]
        [None(typeof(DeleteEntityIntention), typeof(RandomAvatar), typeof(PlayerMoveToWithDurationIntent))]
        private void ResolveVelocity(
            [Data] float dt,
            [Data] int physicsTick,
            [Data] in CameraComponent cameraComponent,
            ref ICharacterControllerSettings settings,
            ref CharacterRigidTransform rigidTransform,
            ref CharacterController characterController,
            ref JumpInputComponent jumpInput,
            ref JumpState jumpState,
            ref GlideState glideState,
            in MovementInputComponent movementInput,
            in MovementSpeedLimit speedLimit) =>
            ResolveAvatarVelocity(dt,
                physicsTick,
                in cameraComponent,
                ref settings,
                ref rigidTransform,
                ref characterController,
                ref jumpInput,
                ref jumpState,
                ref glideState,
                in movementInput,
                in speedLimit,
                cameraComponent.Camera.transform);

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
            ref JumpInputComponent jumpInput,
            ref JumpState jumpState,
            ref GlideState glideState,
            in MovementInputComponent movementInput,
            in MovementSpeedLimit speedLimit) =>
            ResolveAvatarVelocity(dt,
                physicsTick,
                in cameraComponent,
                ref settings,
                ref rigidTransform,
                ref characterController,
                ref jumpInput,
                ref jumpState,
                ref glideState,
                in movementInput,
                in speedLimit,
                // For random avatars we use the character's transform as viewer pov
                characterController.transform);

        private void ResolveAvatarVelocity([Data] float dt,
            [Data] int physicsTick,
            [Data] in CameraComponent cameraComponent,
            ref ICharacterControllerSettings settings,
            ref CharacterRigidTransform rigidTransform,
            ref CharacterController characterController,
            ref JumpInputComponent jumpInput,
            ref JumpState jumpState,
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

            // External forces must run before gravity so ExternalAcceleration.y is available
            ApplyExternalForce.Execute(settings, ref rigidTransform, dt);

            // Vertical velocity (jump + gravity with effective gravity from external forces)
            ApplyJump.Execute(settings, ref rigidTransform, ref jumpState, ref jumpInput, in movementInput, viewerForward, viewerRight, physicsTick, dt);
            ApplyGravity.Execute(settings, ref rigidTransform, jumpState, in jumpInput, physicsTick, dt);
            ApplyGliding.Execute(settings, in rigidTransform, jumpState, in jumpInput, ref glideState, physicsTick, dt);

            // External impulses must run after gravity so it nullify gravity velocity.y
            ApplyExternalImpulse.Execute(settings, ref rigidTransform);

            // Drag
            ApplyHorizontalAirDrag.Execute(settings, ref rigidTransform, dt);
            ApplyExternalVelocityDragAndClamp.Execute(settings, ref rigidTransform, dt);

            // Rotation
            if (cameraComponent.Mode == CameraMode.FirstPerson)
                ApplyFirstPersonRotation.Execute(ref rigidTransform, in cameraComponent);
            else
                ApplyThirdPersonRotation.Execute(ref rigidTransform, in movementInput);

            if (!settings.EnableCharacterRotationBySlope)
                ApplySlopeConditionalRotation.Execute(ref rigidTransform, in settings);
        }
    }
}
