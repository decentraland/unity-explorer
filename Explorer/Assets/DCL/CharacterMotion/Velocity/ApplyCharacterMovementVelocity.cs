using DCL.CharacterCamera;
using DCL.CharacterMotion.Components;
using DCL.CharacterMotion.Settings;
using Diagnostics.ReportsHandling;
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
            Vector3 cameraRight = cameraTransform.right;
            cameraRight.y = 0;

            float yAxis = GetSpeedLimit(settings, input) * input.Axes.y;
            float xAxis = GetSpeedLimit(settings, input) * input.Axes.x;

            int targetAccelerationWeight = Mathf.Abs(xAxis) > 0 || Mathf.Abs(yAxis) > 0 ? 1 : 0;
            rigidTransform.accelerationWeight = Mathf.MoveTowards(rigidTransform.accelerationWeight, targetAccelerationWeight, dt / settings.AccelerationTime);
            float currentAcceleration = Mathf.Lerp(settings.Acceleration, settings.MaxAcceleration, settings.AccelerationCurve.Evaluate(rigidTransform.accelerationWeight));

            // Apply sign correction for ADAD strafing without loosing momentum
            if (Mathf.Abs(input.Axes.x) > 0)
            {
                rigidTransform.xVelocity = Mathf.Sign(xAxis) * Mathf.Abs(rigidTransform.xVelocity);
                rigidTransform.xVelocity = Mathf.MoveTowards(rigidTransform.xVelocity, xAxis, currentAcceleration * dt);
            }
            else
                rigidTransform.xVelocity = Mathf.SmoothDamp(rigidTransform.xVelocity, xAxis, ref rigidTransform.xDamp, settings.StopTimeSec);

            if (Mathf.Abs(input.Axes.y) > 0)
            {
                rigidTransform.zVelocity = Mathf.Sign(yAxis) * Mathf.Abs(rigidTransform.zVelocity);
                rigidTransform.zVelocity = Mathf.MoveTowards(rigidTransform.zVelocity, yAxis, currentAcceleration * dt);
            }
            else
                rigidTransform.zVelocity = Mathf.SmoothDamp(rigidTransform.zVelocity, xAxis, ref rigidTransform.zDamp, settings.StopTimeSec);

            Vector3 targetForward = (cameraForward * rigidTransform.zVelocity) + (cameraRight * rigidTransform.xVelocity);
            rigidTransform.MoveVelocity.Target = targetForward;
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
