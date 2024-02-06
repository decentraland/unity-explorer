using System;
using UnityEngine;

namespace DCL.Multiplayer.Movement.Channel
{
    public class DemoMovementOutputChannel : IMovementOutputChannel
    {
        private Vector2 lastUpdated;

        public void Apply(Vector2 pose)
        {
            lastUpdated = pose;
        }

        public Vector2 Pose()
        {
            return lastUpdated;
        }
    }
}
