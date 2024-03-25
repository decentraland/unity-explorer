using DCL.Character.Components;
using UnityEngine;

namespace DCL.Multiplayer.Movement
{
    public static class Interpolation
    {
        public static float Execute(float deltaTime, ref CharacterTransform transComp, ref InterpolationComponent intComp, float lookAtTimeDelta)
        {
            var remainedDeltaTime = 0f;
            Vector3 lookDirection;

            intComp.Time += deltaTime;

            if (intComp.Time < intComp.TotalDuration)
            {
                transComp.Transform.position = DoTransition(intComp.Start, intComp.End, intComp.Time, intComp.TotalDuration, intComp.SplineType);
                lookDirection = DoTransition(intComp.Start, intComp.End, Mathf.Max(intComp.Time + lookAtTimeDelta, intComp.TotalDuration), intComp.TotalDuration, intComp.SplineType) - transComp.Transform.position; // look into future step
            }
            else
            {
                remainedDeltaTime = intComp.Time - intComp.TotalDuration;

                intComp.Time = intComp.TotalDuration;
                transComp.Transform.position = intComp.End.position;
                lookDirection = intComp.End.velocity;
            }

            LookAt(ref transComp, lookDirection);

            return remainedDeltaTime;
        }

        public static void LookAt(ref CharacterTransform transComp, Vector3 direction)
        {
            // Flattened to have ground plane direction only (XZ)
            direction.y = 0;

            if (direction != Vector3.zero)
            {
                var lookRotation = Quaternion.LookRotation(direction, Vector3.up);
                transComp.Transform.rotation = lookRotation;
            }
        }

        private static Vector3 DoTransition(FullMovementMessage start, FullMovementMessage end, float time, float totalDuration, InterpolationType blendType)
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
