using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.CharacterCamera;
using DCL.CharacterMotion.Components;
using DCL.CharacterMotion.Platforms;
using DCL.CharacterMotion.Settings;
using DCL.Diagnostics;
using ECS.Abstract;
using UnityEngine;
using Utility;

namespace DCL.CharacterMotion.Systems
{
    /// <summary>
    ///     Handles interpolating the character during variable update.
    ///     <para>
    ///         Modifies Transform (by calling `CharacterController` so the value is exposed to other systems ignoring Physics behind
    ///     </para>
    /// </summary>
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [LogCategory(ReportCategory.MOTION)]
    [UpdateBefore(typeof(CameraGroup))]
    public partial class InterpolateCharacterSystem : BaseUnityLoopSystem
    {
        private SingleInstanceEntity fixedTick;
        private SingleInstanceEntity time;

        private InterpolateCharacterSystem(World world) : base(world) { }

        public override void Initialize()
        {
            time = World.CacheTime();
            fixedTick = World.CachePhysicsTick();
        }

        protected override void Update(float t)
        {
            InterpolateQuery(World, t, fixedTick.GetPhysicsTickComponent(World).Tick);
        }

        [Query]
        private void Interpolate(
            [Data] float dt,
            [Data] int physicsTick,
            in ICharacterControllerSettings settings,
            ref CharacterRigidTransform rigidTransform,
            ref CharacterController characterController,
            ref CharacterPlatformComponent platformComponent,
            ref StunComponent stunComponent,
            in JumpInputComponent jump,
            in MovementInputComponent movementInput)
        {
            Vector3 slopeModifier = ApplySlopeModifier.Execute(in settings, in rigidTransform, in movementInput, in jump, characterController, dt);

            ApplyVelocityStun.Execute(ref rigidTransform, in stunComponent);

            Vector3 movementDelta = rigidTransform.MoveVelocity.Velocity * dt;
            Vector3 gravityDelta = rigidTransform.GravityVelocity * dt;

            CollisionFlags horizontalCollisionFlags = characterController.Move(movementDelta);
            CollisionFlags verticalCollisionFlags = characterController.Move(gravityDelta + slopeModifier);

            Debug.DrawLine(characterController.transform.position, characterController.transform.position + (gravityDelta + slopeModifier).normalized, Color.red, dt);

            bool hasGroundedFlag = EnumUtils.HasFlag(verticalCollisionFlags, CollisionFlags.Below) || EnumUtils.HasFlag(horizontalCollisionFlags, CollisionFlags.Below);

            if (!Mathf.Approximately(gravityDelta.y, 0f))
                rigidTransform.IsGrounded = hasGroundedFlag || characterController.isGrounded;

            SaveLocalPosition.Execute(ref platformComponent, characterController.transform.position);
        }
    }
}
