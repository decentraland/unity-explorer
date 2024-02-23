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

        private MessageMock start;
        private float time;
        private Vector3 velocity;

        public ExtrapolationComponent(Transform transform)
        {
            this.transform = transform;

            start = null;

            time = 0f;
            maxDuration = 0f;
            velocity = Vector3.zero;
            Enabled = false;
        }

        public void Update(float deltaTime)
        {
            time += deltaTime;
            velocity = DampVelocity();

            if (velocity.sqrMagnitude > MIN_SPEED)
                transform.position += velocity * deltaTime;
        }

        public void Run(MessageMock from)
        {
            start = from;

            time = 0f;
            velocity = from.velocity;
            maxDuration = LINEAR_EXTRAPOLATION_TIME * DAMPED_EXTRAPOLATION_STEPS;

            Enabled = true;
            Update(Time.deltaTime);
        }

        public MessageMock Stop()
        {
            Enabled = false;

            return new MessageMock
            {
                timestamp = start.timestamp + time,
                position = transform.position,
                velocity = velocity,
            };
        }

        private Vector3 DampVelocity()
        {
            if (time > LINEAR_EXTRAPOLATION_TIME && time < maxDuration)
                return Vector3.Lerp(start.velocity, Vector3.zero, time / maxDuration);

            return time >= maxDuration ? Vector3.zero : velocity;
        }
    }
}
