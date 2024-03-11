using DCL.Multiplayer.Movement.ECS;

namespace DCL.Multiplayer.Movement.Components
{
    public struct InterpolationComponent
    {
        public FullMovementMessage Start;
        public FullMovementMessage End;

        public float Time;
        public float TotalDuration;
        public float SlowDownFactor;

        public InterpolationType SplineType;
        public bool Enabled { get; private set; }

        public void Restart(FullMovementMessage from, FullMovementMessage to, InterpolationType interpolationType)
        {
            SplineType = interpolationType;

            Start = from;
            End = to;

            Time = 0f;
            SlowDownFactor = 1f;
            TotalDuration = End.timestamp - Start.timestamp;

            Enabled = true;
        }

        public void Stop()
        {
            Enabled = false;
        }
    }
}
