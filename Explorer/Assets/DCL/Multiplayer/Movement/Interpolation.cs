using DCL.Character.Components;
using DCL.Multiplayer.Movement.MessageBusMock;
using DCL.Multiplayer.Movement.Settings;
using UnityEngine;

namespace DCL.Multiplayer.Movement.ECS
{
    public static class Interpolation
    {
        public static float Execute(ref CharacterTransform transComp, ref InterpolationComponent comp, float deltaTime, RemotePlayerInterpolationSettings settings)
        {
            var remainedDeltaTime = 0f;
            Vector3 lookDirection;

            comp.Time += deltaTime / comp.SlowDownFactor;

            if (comp.Time >= comp.TotalDuration)
            {
                remainedDeltaTime = (comp.Time - comp.TotalDuration) * comp.SlowDownFactor;

                comp.Time = comp.TotalDuration;
                lookDirection = comp.End.velocity;
            }
            else
            {
                // look into future step
                lookDirection = DoTransition(comp.Start, comp.End, comp.Time + 0.1f, comp.TotalDuration, comp.IsBlend, settings) - transComp.Transform.position;
            }

            LookAt(ref transComp, ref lookDirection);
            transComp.Transform.position = DoTransition(comp.Start, comp.End, comp.Time, comp.TotalDuration, comp.IsBlend, settings);

            return comp.Time == comp.TotalDuration ? remainedDeltaTime : 0;
        }


        private static void LookAt(ref CharacterTransform transComp, ref Vector3 direction)
        {
            // Flattened to have ground plane direction only (XZ)
            direction.y = 0;

            if (direction != Vector3.zero)
            {
                var lookRotation = Quaternion.LookRotation(direction, Vector3.up);
                transComp.Transform.rotation = lookRotation;
            }
        }

        private static Vector3 DoTransition(FullMovementMessage start, FullMovementMessage end, float time, float totalDuration, bool isBlend,
            RemotePlayerInterpolationSettings settings)
        {
            return (isBlend ? settings.BlendType : settings.InterpolationType) switch
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
