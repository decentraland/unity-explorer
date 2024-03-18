namespace DCL.Multiplayer.Movement
{
    public struct InterpolationComponent
    {
        public NetworkMovementMessage Start;
        public NetworkMovementMessage End;

        public float Time;
        public float TotalDuration;

        public InterpolationType SplineType;
        public bool Enabled { get; private set; }

        public void Restart(NetworkMovementMessage from, NetworkMovementMessage to, InterpolationType interpolationType)
        {
            SplineType = interpolationType;

            Start = from;
            End = to;

            Time = 0f;
            TotalDuration = End.timestamp - Start.timestamp;

            Enabled = true;
        }

        public void Stop()
        {
            Enabled = false;
        }
    }
}
