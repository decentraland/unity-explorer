using DCL.Multiplayer.Movement.Settings;
using UnityEngine;

namespace DCL.Multiplayer.Movement.ECS
{
    public struct ExtrapolationComponent
    {
        public FullMovementMessage Start;
        public Vector3 Velocity;

        public float Time;
        public float TotalMoveDuration;

        public bool Enabled;

        public void Restart(FullMovementMessage from, RemotePlayerExtrapolationSettings settings)
        {
            Start = from;
            Velocity = from.velocity;

            Time = 0f;
            TotalMoveDuration = settings.LinearTime + (settings.LinearTime * settings.DampedSteps);

            Enabled = true;
        }

        public void Stop()
        {
            Enabled = false;
        }
    }
}
