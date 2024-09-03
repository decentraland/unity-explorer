using DCL.Character.Components;
using UnityEngine;

namespace DCL.Multiplayer.Movement
{
    public static class Interpolation
    {
        private const float MIN_DIRECTION_SQR_MAGNITUDE = 0.0001f;

        public static float Execute(float deltaTime, ref CharacterTransform transComp, ref InterpolationComponent intComp, float lookAtTimeDelta, float rotationSpeed)
        {
            var remainedDeltaTime = 0f;
            Vector3 lookDirection;

            intComp.Time += deltaTime;

            if (intComp.Time < intComp.TotalDuration)
            {
                transComp.Transform.position = DoTransition(intComp.Start, intComp.End, intComp.Time, intComp.TotalDuration, intComp.SplineType);
                Vector3 nextStep = DoTransition(intComp.Start, intComp.End, Mathf.Max(intComp.Time + lookAtTimeDelta, intComp.TotalDuration), intComp.TotalDuration, intComp.SplineType);
                lookDirection = nextStep - transComp.Transform.position; // look into future step
            }
            else
            {
                remainedDeltaTime = intComp.Time - intComp.TotalDuration;
                intComp.Time = intComp.TotalDuration;

                lookDirection = intComp.End.velocitySqrMagnitude > MIN_DIRECTION_SQR_MAGNITUDE ? intComp.End.velocity : intComp.End.position - transComp.Transform.position;

                transComp.Transform.position = intComp.End.position;
            }

            // Flattened to have ground plane direction only (XZ)
            lookDirection.y = 0;

            if (lookDirection != Vector3.zero)
                LookAt(deltaTime, ref transComp, lookDirection, rotationSpeed, intComp.End.rotationY);

            return remainedDeltaTime;
        }

        private static void LookAt(float dt, ref CharacterTransform transComp, Vector3 direction, float rotationSpeed, float yRotation)
        {
            var lookRotation = Quaternion.LookRotation(direction, Vector3.up);
            lookRotation.eulerAngles = new Vector3(lookRotation.eulerAngles.x, yRotation, lookRotation.eulerAngles.z);
            transComp.Transform.rotation = Quaternion.RotateTowards(transComp.Transform.rotation, lookRotation, rotationSpeed * dt);
        }

        private static Vector3 DoTransition(NetworkMovementMessage start, NetworkMovementMessage end, float time, float totalDuration, InterpolationType blendType)
        {
            return blendType switch
                   {
                       InterpolationType.Linear => InterpolationSpline.Linear(start, end, time, totalDuration),
                       InterpolationType.PositionBlending => InterpolationSpline.ProjectivePositionBlending(start, end, time, totalDuration),
                       InterpolationType.VelocityBlending => InterpolationSpline.ProjectiveVelocityBlending(start, end, time, totalDuration),
                       InterpolationType.Bezier => InterpolationSpline.Bezier(start, end, time, totalDuration),
                       InterpolationType.Hermite => InterpolationSpline.Hermite(start, end, time, totalDuration),
                       InterpolationType.MonotoneYHermite => InterpolationSpline.MonotoneYHermite(start, end, time, totalDuration),
                       InterpolationType.FullMonotonicHermite => InterpolationSpline.FullMonotonicHermite(start, end, time, totalDuration),
                       _ => InterpolationSpline.Linear(start, end, time, totalDuration),
                   };
        }
    }
}
