using UnityEngine;

namespace DCL.Multiplayer.Movement
{
    public enum InterpolationType
    {
        Linear,
        Hermite,
        MonotoneYHermite,
        FullMonotonicHermite,
        Bezier,
        VelocityBlending,
        PositionBlending,
    }

    public static class InterpolationSpline
    {
        /// <summary>
        ///     Linear Interpolation. Just wrapper around built-in Vector3.Lerp
        /// </summary>
        public static Vector3 Linear(NetworkMovementMessage start, NetworkMovementMessage end, float t, float totalDuration) =>
            Vector3.Lerp(start.position, end.position, t / totalDuration);

        /// <summary>
        ///     Interpolation based on Projective Velocity Blending.
        ///     See 'Curtiss Murphy and E Lengyel. Believable dead reckoning for networked games. Game engine gems, 2:307{328, 2011.'
        /// </summary>
        /// <param name="start"> point from which interpolation starts </param>
        /// <param name="end"> point where interpolation should end </param>
        /// <param name="t"> time passed from the start of the interpolation process</param>
        /// <param name="totalDuration"> total duration of the interpolation </param>
        /// <returns></returns>
        public static Vector3 ProjectiveVelocityBlending(NetworkMovementMessage start, NetworkMovementMessage end, float t, float totalDuration)
        {
            Vector3 fakeStartPosition = end.position - (end.velocity * totalDuration);

            float lerpValue = t / totalDuration;

            Vector3 lerpedVelocity = start.velocity + ((end.velocity - start.velocity) * lerpValue); // Interpolated velocity

            // Calculate the position at time t
            Vector3 projectedLocal = start.position + (lerpedVelocity * t);
            Vector3 projectedRemote = fakeStartPosition + (end.velocity * t);

            return projectedLocal + ((projectedRemote - projectedLocal) * lerpValue); // interpolate positions
        }

        /// <summary>
        ///     Interpolation based on Projective Position Blending.
        ///     See 'Curtiss Murphy and E Lengyel. Believable dead reckoning for networked games. Game engine gems, 2:307{328, 2011.'
        /// </summary>
        /// <param name="start"> point from which interpolation starts </param>
        /// <param name="end"> point where interpolation should end </param>
        /// <param name="t"> time passed from the start of the interpolation process</param>
        /// <param name="totalDuration"> total duration of the interpolation </param>
        /// <returns></returns>
        public static Vector3 ProjectivePositionBlending(NetworkMovementMessage start, NetworkMovementMessage end, float t, float totalDuration)
        {
            Vector3 fakeStartPosition = end.position - (end.velocity * totalDuration);

            float lerpValue = t / totalDuration;

            // Calculate the position at time t
            Vector3 projectedLocal = start.position + (start.velocity * t);
            Vector3 projectedRemote = fakeStartPosition + (end.velocity * t);

            return projectedLocal + ((projectedRemote - projectedLocal) * lerpValue); // interpolate positions
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
        public static Vector3 Hermite(NetworkMovementMessage start, NetworkMovementMessage end, float t, float totalDuration)
        {
            // Interpolating
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
        ///     Monotone on Y Cubic Hermite spline interpolation.
        ///     Always pass through the start and end points, but adjust their velocities to ensure monotonicity on Y-axis
        /// </summary>
        /// <param name="start"> point from which interpolation starts </param>
        /// <param name="end"> point where interpolation should end </param>
        /// <param name="t">time passed from the start of the interpolation process</param>
        /// <param name="totalDuration"> total duration of the interpolation </param>
        /// <returns></returns>
        public static Vector3 MonotoneYHermite(NetworkMovementMessage start, NetworkMovementMessage end, float t, float totalDuration)
        {
            (start.velocity.y, end.velocity.y) = Monotonize(start.position.y, end.position.y, start.velocity.y, end.velocity.y, end.timestamp - start.timestamp);
            return Hermite(start, end, t, totalDuration);
        }

        /// <summary>
        ///     Full Monotonic Cubic Hermite spline interpolation.
        ///     Always pass through the start and end points, but adjust their velocities to ensure monotonicity on all axes
        /// </summary>
        /// <param name="start"> point from which interpolation starts </param>
        /// <param name="end"> point where interpolation should end </param>
        /// <param name="t">time passed from the start of the interpolation process</param>
        /// <param name="totalDuration"> total duration of the interpolation </param>
        /// <returns></returns>
        public static Vector3 FullMonotonicHermite(NetworkMovementMessage start, NetworkMovementMessage end, float t, float totalDuration)
        {
            float timeDiff = end.timestamp - start.timestamp;

            (start.velocity.x, end.velocity.x) = Monotonize(start.position.x, end.position.x, start.velocity.x, end.velocity.x, timeDiff);
            (start.velocity.y, end.velocity.y) = Monotonize(start.position.y, end.position.y, start.velocity.y, end.velocity.y, timeDiff);
            (start.velocity.z, end.velocity.z) = Monotonize(start.position.z, end.position.z, start.velocity.z, end.velocity.z, timeDiff);

            return Hermite(start, end, t, totalDuration);
        }

        private static (float, float) Monotonize(float start, float end, float velocityStart, float velocityEnd, float deltaTime)
        {
            float desiredChangeRate = (end - start) / deltaTime;

            if (end > start) // Ensure monotonic increase
            {
                velocityStart = Mathf.Max(velocityStart, 0);
                velocityEnd = Mathf.Max(velocityEnd, 0);

                // Further adjust to not exceed the overall change rate
                velocityStart = Mathf.Min(velocityStart, desiredChangeRate);
                velocityEnd = Mathf.Min(velocityEnd, desiredChangeRate);
            }
            else if (end < start) // Ensure monotonic decrease
            {
                velocityStart = Mathf.Min(velocityStart, 0);
                velocityEnd = Mathf.Min(velocityEnd, 0);

                // Further adjust to not exceed the overall change rate (in magnitude)
                velocityStart = Mathf.Max(velocityStart, desiredChangeRate);
                velocityEnd = Mathf.Max(velocityEnd, desiredChangeRate);
            }

            return (velocityStart, velocityEnd);
        }

        /// <summary>
        ///     Cubic Bézier spline interpolation.
        ///     Always pass through the start and end points, use velocities to define ancors
        /// </summary>
        /// <param name="start"> point from which interpolation starts </param>
        /// <param name="end"> point where interpolation should end </param>
        /// <param name="t">time passed from the start of the interpolation process</param>
        /// <param name="totalDuration"> total duration of the interpolation </param>
        /// <returns></returns>
        public static Vector3 Bezier(NetworkMovementMessage start, NetworkMovementMessage end, float t, float totalDuration)
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
