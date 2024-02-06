using System;
using UnityEngine;

namespace DCL.Multiplayer.Movement.Channel
{
    public class InterpolatedMovementOutputChannel : IMovementOutputChannel
    {
        private readonly IMovementOutputChannel origin;
        private readonly IInterpolateRatio interpolateRatio;
        private (Vector2 pose, float timestamp) lastUpdated;
        private Vector2 interpolated;

        public InterpolatedMovementOutputChannel(IMovementOutputChannel origin, IInterpolateRatio interpolateRatio)
        {
            this.origin = origin;
            this.interpolateRatio = interpolateRatio;
        }

        public Vector2 Pose()
        {
            var current = origin.Pose();
            float time = UnityEngine.Time.time;

            interpolated = Vector2.MoveTowards(
                interpolated,
                lastUpdated.pose,
                Vector2.Distance(interpolated, lastUpdated.pose)
                * (time - lastUpdated.timestamp)
                * interpolateRatio.Value
            );

            if (lastUpdated.pose != current)
                lastUpdated = (current, time);

            return interpolated;
        }

        public interface IInterpolateRatio
        {
            float Value { get; }
        }
    }
}
