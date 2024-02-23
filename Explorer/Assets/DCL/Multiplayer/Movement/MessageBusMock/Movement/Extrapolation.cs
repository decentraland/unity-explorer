using UnityEngine;

namespace DCL.Multiplayer.Movement.MessageBusMock.Movement
{
    public class Extrapolation
    {
        private readonly Transform transform;

        public bool IsRunning;

        private MessageMock start;
        private MessageMock end;

        private float time;
        private Vector3 velocity;

        private float minSpeed;
        private float totalDuration;

        public Extrapolation(Transform transform)
        {
            this.transform = transform;
        }

        public void Run(MessageMock from, float packageSentRate, float minSpeed, bool lastStep = false)
        {
            start = from;

            end = new MessageMock
            {
                timestamp = from.timestamp + packageSentRate,
                position = from.position + (from.velocity * packageSentRate),
                velocity = lastStep ? from.velocity / 2 : Vector3.zero,
            };

            totalDuration = packageSentRate;

            velocity = start.velocity;
            time = 0f;
            this.minSpeed = minSpeed;

            IsRunning = true;
        }

        public void Run(MessageMock from, MessageMock to, float minSpeed)
        {
            start = from;

            end = to;
            totalDuration = to.timestamp - from.timestamp;

            velocity = start.velocity;
            time = 0f;
            this.minSpeed = minSpeed;

            IsRunning = true;
        }

        public void Update(float deltaTime)
        {
            time += deltaTime;

            // velocity = DampVelocity();
            if (velocity.sqrMagnitude < minSpeed)
                return;

            if (time < totalDuration)
            {
                transform.position = Interpolate.Hermite(start, end, time, totalDuration);
                velocity = start.velocity + ((end.velocity - start.velocity) * time / totalDuration);
            }
            else
            {
                transform.position = end.position;
                velocity = end.velocity;
                Run(Stop(), totalDuration, minSpeed, true);
            }
        }

        public MessageMock Stop()
        {
            IsRunning = false;

            return new MessageMock
            {
                timestamp = start.timestamp + time,
                position = transform.position,
                velocity = velocity,
            };
        }
    }
}
