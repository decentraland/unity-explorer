using DCL.Multiplayer.Movement.MessageBusMock;
using UnityEngine;

namespace DCL.Multiplayer.Movement.ECS
{
    public struct ExtrapolationComponent
    {
        private const float MIN_SPEED = 0.01f;
        private const float LINEAR_EXTRAPOLATION_TIME = 0.33f;
        private const int DAMPED_EXTRAPOLATION_STEPS = 2;

        private readonly Transform transform;
        private float maxDuration;

        public bool Enabled;

        public MessageMock Start;
        public float Time;
        private Vector3 velocity;

        public ExtrapolationComponent(Transform transform)
        {
            this.transform = transform;

            Start = null;

            Time = 0f;
            maxDuration = 0f;
            velocity = Vector3.zero;
            Enabled = false;
        }

        public void Update(float deltaTime)
        {
            Time += deltaTime;
            velocity = DampVelocity();

            if (velocity.sqrMagnitude > MIN_SPEED)
                transform.position += velocity * deltaTime;
        }

        public void Run(MessageMock from)
        {
            Start = from;

            Time = 0f;
            velocity = from.velocity;
            maxDuration = LINEAR_EXTRAPOLATION_TIME * DAMPED_EXTRAPOLATION_STEPS;

            Enabled = true;
        }

        public MessageMock Stop()
        {
            Enabled = false;

            return new MessageMock
            {
                timestamp = Start.timestamp + Time,
                position = transform.position,
                velocity = velocity,
            };
        }

        private Vector3 DampVelocity()
        {
            if (Time > LINEAR_EXTRAPOLATION_TIME && Time < maxDuration)
                return Vector3.Lerp(Start.velocity, Vector3.zero, Time / maxDuration);

            return Time >= maxDuration ? Vector3.zero : velocity;
        }
    }
}
