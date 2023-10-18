using DCL.CharacterCamera;
using DCL.CharacterMotion.Components;
using DCL.CharacterMotion.Settings;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace DCL.CharacterMotion
{
    public static class ApplyCharacterMovementVelocity
    {
        public static void Execute(ICharacterControllerSettings settings,
            ref CharacterRigidTransform rigidTransform,
            in CameraComponent camera,
            in MovementInputComponent input,
            float dt)
        {
            Transform cameraTransform = camera.Camera.transform;

            Vector3 cameraForward = cameraTransform.forward;
            cameraForward.y = 0;

            // Normalize the forward to avoid slower forward speeds when looking up/down
            cameraForward.Normalize();
            Vector3 cameraRight = cameraTransform.right;
            cameraRight.y = 0;

            float speedLimit = GetSpeedLimit(settings, input);
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
            rigidTransform.MoveVelocity.Velocity = targetForward;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float GetAcceleration(CharacterRigidTransform rigidTransform, ICharacterControllerSettings settings, CharacterRigidTransform.MovementVelocity moveVelocity)
        {
            if (rigidTransform.IsGrounded)
                return Mathf.Lerp(settings.Acceleration, settings.MaxAcceleration, settings.AccelerationCurve.Evaluate(moveVelocity.AccelerationWeight));
            else
                return Mathf.Lerp(settings.AirAcceleration, settings.MaxAirAcceleration, settings.AccelerationCurve.Evaluate(moveVelocity.AccelerationWeight));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float GetSpeedLimit(ICharacterControllerSettings settings, in MovementInputComponent inputComponent)
        {
            switch (inputComponent.Kind)
            {
                case MovementKind.Run:
                    return settings.RunSpeed;
                case MovementKind.Jog:
                    return settings.JogSpeed;
                default: return settings.WalkSpeed;
            }
        }
    }
}
