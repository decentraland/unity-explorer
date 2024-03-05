using DCL.Multiplayer.Movement.MessageBusMock;
using DCL.Multiplayer.Movement.Settings;
using UnityEngine;

namespace DCL.Multiplayer.Movement.ECS
{
    public struct ExtrapolationComponent
    {
        private readonly Transform transform;
        public float TotalMoveDuration;

        public bool Enabled;

        public MessageMock Start;
        public float Time;
        private Vector3 velocity;
        private IMultiplayerSpatialStateSettings settings;

        public ExtrapolationComponent(Transform transform)
        {
            this.transform = transform;

            Start = null;

            Time = 0f;
            TotalMoveDuration = 0f;
            velocity = Vector3.zero;
            Enabled = false;

            settings = null;
        }

        public void Update(float deltaTime)
        {
            Time += deltaTime;
            velocity = DampVelocity(Time, Start, settings);

            if (velocity.sqrMagnitude > settings.MinSpeed)
                transform.position += velocity * deltaTime;
        }

        public void Run(MessageMock from, IMultiplayerSpatialStateSettings settings)
        {
            this.settings = settings;
            Start = from;

            Time = 0f;
            velocity = from.velocity;
            TotalMoveDuration = this.settings.LinearTime + (this.settings.LinearTime * this.settings.DampedSteps);

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

        public static Vector3 DampVelocity(float time, MessageMock start, IMultiplayerSpatialStateSettings settings)
        {
            float totalMoveDuration = settings.LinearTime + (settings.LinearTime * settings.DampedSteps);

            if (time > settings.LinearTime && time < totalMoveDuration)
                return Vector3.Lerp(start.velocity, Vector3.zero, time / totalMoveDuration);

            return time >= totalMoveDuration ? Vector3.zero : start.velocity;
        }
    }
}
