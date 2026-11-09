using DCL.ECSComponents;
using System;
using UnityEngine;

namespace DCL.SkyBox
{
    /// <summary>
    ///     The sun values of a scene that live outside the look preset: the multiplier on the time-of-day light intensity
    ///     and the fixed direction the light travels. The controller applies them after every light evaluation, so a
    ///     change needs no preset rebuild. The default value is neutral: the time-of-day intensity and path.
    /// </summary>
    public readonly struct SceneSunOverride : IEquatable<SceneSunOverride>
    {
        public static readonly SceneSunOverride NONE = default(SceneSunOverride);

        // Shorter directions carry no usable orientation and fall back to the time-of-day path.
        private const float MIN_DIRECTION_SQR_MAGNITUDE = 1e-6f;

        /// <summary>
        ///     Multiplier on the time-of-day light intensity, never negative. Null keeps the time-of-day intensity.
        /// </summary>
        public readonly float? Intensity;

        /// <summary>
        ///     Normalized direction the sunlight travels in scene space, fixed at every time of day. Null follows the time-of-day path.
        /// </summary>
        public readonly Vector3? Direction;

        private SceneSunOverride(float? intensity, Vector3? direction)
        {
            Intensity = intensity;
            Direction = direction;
        }

        /// <summary>
        ///     Neutral for a missing component; a non-finite intensity or direction counts as unset.
        /// </summary>
        public static SceneSunOverride FromProto(PBSun? pbSun)
        {
            if (pbSun == null) return NONE;

            float? intensity = pbSun.HasIntensity ? SceneEnvironmentProfile.Finite(pbSun.Intensity, 0f, float.MaxValue) : null;
            return new SceneSunOverride(intensity, ToDirection(pbSun.Direction));
        }

        public bool Equals(SceneSunOverride other) =>
            Nullable.Equals(Intensity, other.Intensity) && Nullable.Equals(Direction, other.Direction);

        public override bool Equals(object? obj) =>
            obj is SceneSunOverride other && Equals(other);

        public override int GetHashCode() =>
            HashCode.Combine(Intensity, Direction);

        private static Vector3? ToDirection(Decentraland.Common.Vector3? direction)
        {
            if (direction == null) return null;

            if (!float.IsFinite(direction.X) || !float.IsFinite(direction.Y) || !float.IsFinite(direction.Z))
                return null;

            var unityDirection = new Vector3(direction.X, direction.Y, direction.Z);

            return unityDirection.sqrMagnitude < MIN_DIRECTION_SQR_MAGNITUDE ? null : unityDirection.normalized;
        }
    }
}
