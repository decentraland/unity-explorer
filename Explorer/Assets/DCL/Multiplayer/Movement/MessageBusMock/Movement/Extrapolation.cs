using UnityEngine;

namespace DCL.Multiplayer.Movement.MessageBusMock.Movement
{
    public class Extrapolation : MonoBehaviour
    {
        public float minSpeed = 0.1f;
        public float linearExtrapolationTime = 0.33f;
        public int dampedExtrapolationSteps = 2;

        [Space]
        public float Time;
        public Vector3 Velocity;

        private MessageMock start;
        private float maxDuration;

        private void Update()
        {
            Time += UnityEngine.Time.deltaTime;

            Velocity = DampVelocity();

            if (Velocity.sqrMagnitude > minSpeed)
                transform.position += Velocity * UnityEngine.Time.deltaTime;
        }

        private void OnEnable()
        {
            Time = 0f;
            Velocity = start.velocity;
            maxDuration = linearExtrapolationTime * dampedExtrapolationSteps;
        }

        public void Run(MessageMock from)
        {
            start = from;
            enabled = true;
        }

        public MessageMock Stop()
        {
            enabled = false;

            return new MessageMock
            {
                timestamp = start.timestamp + Time,
                position = transform.position,
                velocity = Velocity,
                acceleration = Vector3.zero,
            };
        }

        private Vector3 DampVelocity()
        {
            if (Time > linearExtrapolationTime && Time < maxDuration)
                return Vector3.Lerp(start.velocity, Vector3.zero, Time / maxDuration);

            return Time >= maxDuration ? Vector3.zero : Velocity;
        }
    }
}
