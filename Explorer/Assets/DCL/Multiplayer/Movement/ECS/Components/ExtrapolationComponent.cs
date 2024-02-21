using DCL.Multiplayer.Movement.MessageBusMock;
using UnityEngine;

namespace DCL.Multiplayer.Movement.ECS
{
    public struct ExtrapolationComponent
    {
        private const float MIN_SPEED = 0.01f;
        private const float LINEAR_EXTRAPOLATION_TIME = 0.33f;
        private const int DAMPED_EXTRAPOLATION_STEPS = 2;

        public readonly Transform Transform;
        private readonly float maxDuration;

        public bool Enabled;

        public MessageMock Start;
        public float Time;
        public Vector3 Velocity;

        public ExtrapolationComponent(Transform transform)
        {
            Transform = transform;

            Start = null;
            maxDuration = LINEAR_EXTRAPOLATION_TIME * DAMPED_EXTRAPOLATION_STEPS;

            Time = 0f;
            Velocity = Vector3.zero;
            Enabled = false;
        }

        public void Update(float deltaTime)
        {
            Time += deltaTime;
            Velocity = DampVelocity();

            if (Velocity.sqrMagnitude > MIN_SPEED)
                Transform.position += Velocity * UnityEngine.Time.deltaTime;
        }

        public void Run(MessageMock from)
        {
            Start = from;

            Time = 0f;
            Velocity = from.velocity;
            Enabled = true;
        }

        private Vector3 DampVelocity()
        {
            if (Time > LINEAR_EXTRAPOLATION_TIME && Time < maxDuration)
                return Vector3.Lerp(Start.velocity, Vector3.zero, Time / maxDuration);

            return Time >= maxDuration ? Vector3.zero : Velocity;
        }
    }
}
