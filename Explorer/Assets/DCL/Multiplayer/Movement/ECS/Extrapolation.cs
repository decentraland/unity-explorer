using DCL.Character.Components;
using DCL.Multiplayer.Movement.Settings;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace DCL.Multiplayer.Movement.ECS
{
    public static class Extrapolation
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Execute(ref CharacterTransform transComp, ref ExtrapolationComponent ext, float deltaTime, RemotePlayerExtrapolationSettings settings)
        {
            ext.Time += deltaTime;
            ext.Velocity = DampVelocity(ext.Time, ext.Start.velocity, settings);

            if (ext.Velocity.sqrMagnitude > settings.MinSpeed)
                transComp.Transform.position += ext.Velocity * deltaTime;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector3 DampVelocity(float time, Vector3 velocity, RemotePlayerExtrapolationSettings settings)
        {
            float totalMoveDuration = settings.LinearTime + (settings.LinearTime * settings.DampedSteps);

            if (time > settings.LinearTime && time < totalMoveDuration)
                return Vector3.Lerp(velocity, Vector3.zero, time / totalMoveDuration);

            return time >= totalMoveDuration ? Vector3.zero : velocity;
        }
    }
}
