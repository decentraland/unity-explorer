using UnityEngine;

namespace DCL.Multiplayer.Movement
{
    public struct ExtrapolationComponent
    {
        public NetworkMovementMessage Start;
        public Vector3 Velocity;

        public float Time;
        public float TotalMoveDuration;

        public bool Enabled { get; private set; }

        public void Restart(NetworkMovementMessage from, float moveDuration)
        {
            Start = from;
            Velocity = from.velocity;

            Time = 0f;
            TotalMoveDuration = moveDuration;

            Enabled = true;
        }

        public void Stop()
        {
            Enabled = false;
        }
    }
}
