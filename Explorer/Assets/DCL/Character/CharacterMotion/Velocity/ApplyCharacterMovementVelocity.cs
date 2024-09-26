
using DCL.CharacterMotion.Components;
using DCL.CharacterMotion.Settings;
using DCL.CharacterMotion.Utils;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace DCL.CharacterMotion
{
    public static class ApplyCharacterMovementVelocity
    {
        public static void Execute(ICharacterControllerSettings settings,
            ref CharacterRigidTransform rigidTransform,
            Transform cameraTransform,
            in MovementInputComponent input,
            float dt)
        {
            Vector3 cameraForward = cameraTransform.forward;
            cameraForward.y = 0;

            // Normalize the forward to avoid slower forward speeds when looking up/down
            cameraForward.Normalize();
            Vector3 cameraRight = cameraTransform.right;
            cameraRight.y = 0;

            float speedLimit = SpeedLimit.Get(settings, input.Kind);

            // Apply movement speed increase/decrease based on the current slope angle
            var slopeForward = Vector3.Cross(rigidTransform.CurrentSlopeNormal, Vector3.Cross(rigidTransform.LookDirection, rigidTransform.CurrentSlopeNormal));
            float slopeAngle = Vector3.SignedAngle(rigidTransform.LookDirection, slopeForward, Vector3.Cross(rigidTransform.LookDirection, Vector3.up));
            float movementSpeedModifier = settings.SlopeVelocityModifier.Evaluate(slopeAngle);
            speedLimit *= movementSpeedModifier;

            float yAxis = speedLimit * input.Axes.y;
            float xAxis = speedLimit * input.Axes.x;

            int targetAccelerationWeight = Mathf.Abs(xAxis) > 0 || Mathf.Abs(yAxis) > 0 ? 1 : 0;
            rigidTransform.MoveVelocity.AccelerationWeight = Mathf.MoveTowards(rigidTransform.MoveVelocity.AccelerationWeight, targetAccelerationWeight, dt / settings.AccelerationTime);
            float currentAcceleration = GetAcceleration(rigidTransform, settings, rigidTransform.MoveVelocity);

            // Apply sign correction for ADAD strafing without loosing momentum
            if (Mathf.Abs(input.Axes.x) > 0)
            {
                rigidTransform.MoveVelocity.XVelocity = Mathf.Sign(xAxis) * Mathf.Abs(rigidTransform.MoveVelocity.XVelocity);
                rigidTransform.MoveVelocity.XVelocity = Mathf.MoveTowards(rigidTransform.MoveVelocity.XVelocity, xAxis, currentAcceleration * dt);
            }
            else
                rigidTransform.MoveVelocity.XVelocity = Mathf.SmoothDamp(rigidTransform.MoveVelocity.XVelocity, xAxis, ref rigidTransform.MoveVelocity.XDamp, settings.StopTimeSec);

            if (Mathf.Abs(input.Axes.y) > 0)
            {
                rigidTransform.MoveVelocity.ZVelocity = Mathf.Sign(yAxis) * Mathf.Abs(rigidTransform.MoveVelocity.ZVelocity);
                rigidTransform.MoveVelocity.ZVelocity = Mathf.MoveTowards(rigidTransform.MoveVelocity.ZVelocity, yAxis, currentAcceleration * dt);
            }
            else
                rigidTransform.MoveVelocity.ZVelocity = Mathf.SmoothDamp(rigidTransform.MoveVelocity.ZVelocity, yAxis, ref rigidTransform.MoveVelocity.ZDamp, settings.StopTimeSec);

            Vector3 targetForward = (cameraForward * rigidTransform.MoveVelocity.ZVelocity) + (cameraRight * rigidTransform.MoveVelocity.XVelocity);
            targetForward = Vector3.ClampMagnitude(targetForward, speedLimit);

            if (rigidTransform.IsGrounded && !rigidTransform.IsOnASteepSlope)
            {
                // Grounded velocity change is instant
                rigidTransform.MoveVelocity.Velocity = targetForward;
            }
            else
            {
                // Air velocity change is updated slowly in order for drag to work, in the real world the velocity should not increase every frame because we cant "move" in the air, but we do here
                rigidTransform.MoveVelocity.Velocity = Vector3.MoveTowards(rigidTransform.MoveVelocity.Velocity, targetForward, currentAcceleration * dt);
            }
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float GetAcceleration(CharacterRigidTransform rigidTransform, ICharacterControllerSettings settings, CharacterRigidTransform.MovementVelocity moveVelocity)
        {
            if (rigidTransform.IsGrounded && !rigidTransform.IsOnASteepSlope)
                return Mathf.Lerp(settings.Acceleration, settings.MaxAcceleration, settings.AccelerationCurve.Evaluate(moveVelocity.AccelerationWeight));
            else
                return Mathf.Lerp(settings.AirAcceleration, settings.MaxAirAcceleration, settings.AccelerationCurve.Evaluate(moveVelocity.AccelerationWeight));
        }
    }
}
