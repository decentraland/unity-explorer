using System;
using UnityEngine;

namespace DCL.Multiplayer.Movement.MessageBusMock
{
    public class InterpolationMonoBeh : MonoBehaviour
    {
        public Receiver receiver;

        public InterpolationType interpolationType;
        public float minPositionDelta = 0.1f;

        [Space]
        public float Time;

        public bool IsProjected;

        private MessageMock start;
        private MessageMock end;
        private float totalDuration;

        private Func<MessageMock, MessageMock, float, float, Vector3> interpolation;
        private bool isFirst = true;

        public event Action<MessageMock> PointPassed;

        public float CurrentTimestamp => start.timestamp + Time;

        private void Update()
        {
            Time += UnityEngine.Time.deltaTime;

            if (Time < totalDuration)
                transform.position = interpolation(start, end, Time, totalDuration);
            else
                End();
        }

        private void Go()
        {
            Time = 0f;

            float timeDiff = end.timestamp - start.timestamp;
            float correctionTime = receiver.IncomingMessages.Count * UnityEngine.Time.smoothDeltaTime;
            totalDuration = Mathf.Max(timeDiff - correctionTime, timeDiff / 4f);

            interpolation = GetInterpolationFunc(interpolationType);

            enabled = true;
        }

        private void End()
        {
            enabled = false;
            IsProjected = false;

            transform.position = end.position;
            PointPassed?.Invoke(end);
            isFirst = false;
        }

        public MessageMock Stop()
        {
            enabled = false;
            IsProjected = false;

            return new MessageMock
            {
                timestamp = start.timestamp + Time,
                position = transform.position,
                velocity = start.velocity + ((end.velocity - start.velocity) * (Time / (end.timestamp - start.timestamp))),
            };
        }

        public void Run(MessageMock from, MessageMock to, bool isProjected = false)
        {
            IsProjected = isProjected;

            if (from?.timestamp > to.timestamp) return;

            start = from;
            end = to;

            // Can skip
            if (isFirst || (Vector3.Distance(from.position, to.position) < minPositionDelta && receiver.IncomingMessages.Count > 0))
                End();
            else
                Go();
        }

        private static Func<MessageMock, MessageMock, float, float, Vector3> GetInterpolationFunc(InterpolationType type)
        {
            return type switch
                   {
                       InterpolationType.Linear => Interpolate.Linear,
                       InterpolationType.Hermite => Interpolate.Hermite,
                       InterpolationType.Bezier => Interpolate.Bezier,
                       InterpolationType.VelocityBlending => Interpolate.ProjectiveVelocityBlending,
                       _ => Interpolate.Linear,
                   };
        }
    }
}
