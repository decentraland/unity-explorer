using DCL.Character.Components;
using DCL.Multiplayer.Movement.Settings;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace DCL.Multiplayer.Movement
{
    public static class Extrapolation
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Execute(float deltaTime, ref CharacterTransform transComp, ref ExtrapolationComponent ext, RemotePlayerExtrapolationSettings settings)
        {
            ext.Time += deltaTime;
            ext.Velocity = DampVelocity(ext.Start.velocity, ext.Time, ext.TotalMoveDuration, settings.LinearTime);

            if (ext.Velocity.sqrMagnitude > settings.MinSpeed)
            {
                var newPosition = transComp.Transform.position + (ext.Velocity * deltaTime);

                // Clamp the Y position to avoid passing the floor (for both cases - above and below the floor)
                if (transComp.Transform.position.y * newPosition.y <= 0)
                {
                    newPosition.y = 0;
                    ext.Velocity.y = 0;
                }

                transComp.Transform.position = newPosition;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector3 DampVelocity(Vector3 velocity, float time, float totalMoveDuration, float linearTime)
        {
            if (time > linearTime && time < totalMoveDuration)
            {
                float dampDuration = totalMoveDuration - linearTime;
                float dampTime = time - linearTime;

                return Vector3.Lerp(velocity, Vector3.zero, dampTime / dampDuration);
            }

            return time >= totalMoveDuration ? Vector3.zero : velocity;
        }
    }
}
