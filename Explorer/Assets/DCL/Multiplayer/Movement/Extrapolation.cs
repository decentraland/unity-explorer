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
                transComp.Transform.position += ext.Velocity * deltaTime;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector3 DampVelocity(Vector3 velocity, float time, float totalMoveDuration, float linearTime)
        {
            if (time > linearTime && time < totalMoveDuration)
                return Vector3.Lerp(velocity, Vector3.zero, time / totalMoveDuration);

            return time >= totalMoveDuration ? Vector3.zero : velocity;
        }
    }
}
