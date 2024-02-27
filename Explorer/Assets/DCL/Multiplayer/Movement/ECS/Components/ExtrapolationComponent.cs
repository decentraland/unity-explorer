using DCL.Multiplayer.Movement.MessageBusMock;
using UnityEngine;

namespace DCL.Multiplayer.Movement.ECS
{
    public struct ExtrapolationComponent
    {
        private readonly Transform transform;
        private float totalMoveDuration;

        public bool Enabled;

        public MessageMock Start;
        public float Time;
        private Vector3 velocity;
        private MessagePipeSettings settings;

        public ExtrapolationComponent(Transform transform)
        {
            this.transform = transform;

            Start = null;

            Time = 0f;
            totalMoveDuration = 0f;
            velocity = Vector3.zero;
            Enabled = false;

            settings = null;
        }

        public void Update(float deltaTime)
        {
            Time += deltaTime;
            velocity = DampVelocity();

            if (velocity.sqrMagnitude > settings.MinSpeed)
                transform.position += velocity * deltaTime;
        }

        public void Run(MessageMock from, MessagePipeSettings settings)
        {
            this.settings = settings;
            Start = from;

            Time = 0f;
            velocity = from.velocity;
            totalMoveDuration = this.settings.LinearTime + (this.settings.LinearTime * this.settings.DampedSteps);

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
            if (Time > settings.LinearTime && Time < totalMoveDuration)
                return Vector3.Lerp(Start.velocity, Vector3.zero, Time / totalMoveDuration);

            return Time >= totalMoveDuration ? Vector3.zero : velocity;
        }
    }
}
