using System;
using UnityEngine;

namespace DCL.Multiplayer.Movement.MessageBusMock
{
    public enum InterpolationType
    {
        Linear,
        Hermite,
        Bezier,
        VelocityBlending,
        PositionBlending,
    }

    public static class Interpolate
    {
        public static Func<MessageMock, MessageMock, float, float, Vector3> GetInterpolationFunc(InterpolationType type)
        {
            return type switch
                   {
                       InterpolationType.Linear => Linear,
                       InterpolationType.Hermite => Hermite,
                       InterpolationType.Bezier => Bezier,
                       InterpolationType.VelocityBlending => ProjectiveVelocityBlending,
                       InterpolationType.PositionBlending => ProjectivePositionBlending,
                       _ => Linear,
                   };
        }

        /// <summary>
        ///     Linear Interpolation. Just wrapper around built-in Vector3.Lerp
        /// </summary>
        public static Vector3 Linear(MessageMock start, MessageMock end, float t, float totalDuration) =>
            Vector3.Lerp(start.position, end.position, t / totalDuration);

        /// <summary>
        ///     Interpolation based on Projective Velocity Blending.
        ///     Ensures that the velocity and position of the end point are matched at the end of interpolation.
        ///     See 'Curtiss Murphy and E Lengyel. Believable dead reckoning for networked games. Game engine gems, 2:307{328, 2011.'
        /// </summary>
        /// <param name="start"> point from which interpolation starts </param>
        /// <param name="end"> point where interpolation should end </param>
        /// <param name="t"> time passed from the start of the interpolation process</param>
        /// <param name="totalDuration"> total duration of the interpolation </param>
        /// <returns></returns>
        public static Vector3 ProjectiveVelocityBlending(MessageMock start, MessageMock end, float t, float totalDuration)
        {
            Vector3 fakeStartPosition = end.position - (end.velocity * totalDuration); // project in past

            float lerpValue = t / totalDuration;

            Vector3 lerpedVelocity = start.velocity + ((end.velocity - start.velocity) * lerpValue); // Interpolated velocity

            // Calculate the position at time t
            Vector3 projectedLocal = start.position + (lerpedVelocity * t);
            Vector3 projectedRemote = fakeStartPosition + (end.velocity * t);

            return projectedLocal + ((projectedRemote - projectedLocal) * lerpValue); // interpolate positions
        }

        /// <summary>
        ///     Interpolation based on Projective Position Blending.
        /// </summary>
        /// <param name="start"> point from which interpolation starts </param>
        /// <param name="end"> point where interpolation should end </param>
        /// <param name="t"> time passed from the start of the interpolation process</param>
        /// <param name="totalDuration"> total duration of the interpolation </param>
        /// <returns></returns>
        public static Vector3 ProjectivePositionBlending(MessageMock start, MessageMock end, float t, float totalDuration)
        {
            Vector3 fakeStartPosition = end.position - (end.velocity * totalDuration); // project in past

            // Calculate the position at time t
            Vector3 projectedLocal = start.position + (start.velocity * t);
            Vector3 projectedRemote = fakeStartPosition + (end.velocity * t);

            return projectedLocal + ((projectedRemote - projectedLocal) * (t / totalDuration)); // interpolate positions
        }

        /// <summary>
        ///     Cubic Hermite spline interpolation.
        ///     Always pass through the start and end points and match their velocities
        /// </summary>
        /// <param name="start"> point from which interpolation starts </param>
        /// <param name="end"> point where interpolation should end </param>
        /// <param name="t">time passed from the start of the interpolation process</param>
        /// <param name="totalDuration"> total duration of the interpolation </param>
        /// <returns></returns>
        public static Vector3 Hermite(MessageMock start, MessageMock end, float t, float totalDuration)
        {
            float lerpValue = t / totalDuration;

            float t2 = lerpValue * lerpValue;
            float t3 = t2 * lerpValue;

            float h2 = (-2 * t3) + (3 * t2); // Hermite basis function h_01 (for end position)
            float h1 = -h2 + 1; // Hermite basis function h_00 (for start position)

            float h3 = t3 - (2 * t2) + lerpValue; // Hermite basis function h_10 (for start velocity)
            float h4 = t3 - t2; // Hermite basis function h_11 (for end velocity)

            // note: (start.velocity * timeDif) and (end.velocity * timeDif) can be cached
            return (h1 * start.position) + (h2 * end.position) + (start.velocity * (h3 * totalDuration)) + (end.velocity * (h4 * totalDuration));
        }

        /// <summary>
        ///     Cubic Bézier spline interpolation.
        ///     Always pass through the start and end points and match their velocities
        /// </summary>
        /// <param name="start"> point from which interpolation starts </param>
        /// <param name="end"> point where interpolation should end </param>
        /// <param name="t">time passed from the start of the interpolation process</param>
        /// <param name="totalDuration"> total duration of the interpolation </param>
        /// <returns></returns>
        public static Vector3 Bezier(MessageMock start, MessageMock end, float t, float totalDuration)
        {
            float lerpValue = t / totalDuration;

            // Compute the control points based on start and end positions and velocities
            // note: c0 and c1 can be cached
            Vector3 c0 = start.position + (start.velocity * (totalDuration / 3));
            Vector3 c1 = end.position - (end.velocity * (totalDuration / 3));

            float t2 = lerpValue * lerpValue;
            float t3 = t2 * lerpValue;

            float oneMinusT = 1 - lerpValue;
            float oneMinusT2 = oneMinusT * oneMinusT;
            float oneMinusT3 = oneMinusT2 * oneMinusT;

            return (oneMinusT3 * start.position) + (3 * oneMinusT2 * lerpValue * c0) + (3 * oneMinusT * t2 * c1) + (t3 * end.position);
        }
    }
}
