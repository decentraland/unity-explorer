using System;
using UnityEngine;
using UnityEngine.Serialization;

namespace DCL.Multiplayer.Movement.MessageBusMock
{
    public class Interpolation : MonoBehaviour
    {
        public Receiver receiver;

        public InterpolationType interpolationType;
        public float minPositionDelta = 0.1f;

        [Space]
        public float Time;

        private MessageMock start;
        private MessageMock end;
        private float totalDuration;

        private Func<MessageMock, MessageMock, float, float, Vector3> interpolation;
        private bool isFirst = true;

        public event Action<MessageMock> PointPassed;

        private void Update()
        {
            Time += UnityEngine.Time.deltaTime;

            if (Time < totalDuration)
                transform.position = interpolation(start, end, Time, totalDuration);
            else
                enabled = false;
        }

        private void OnEnable()
        {
            Time = 0f;

            float timeDiff = end.timestamp - start.timestamp;
            float correctionTime = receiver.IncomingMessages.Count * UnityEngine.Time.smoothDeltaTime;
            totalDuration = Mathf.Max(timeDiff - correctionTime, timeDiff / 4f);

            interpolation = interpolationType switch
                            {
                                InterpolationType.Linear => Interpolate.Linear,
                                InterpolationType.Hermite => Interpolate.Hermite,
                                InterpolationType.Bezier => Interpolate.Bezier,
                                InterpolationType.VelocityBlending => Interpolate.ProjectiveVelocityBlending,
                                _ => Interpolate.Linear,
                            };
        }

        private void OnDisable()
        {
            transform.position = end.position;
            PointPassed?.Invoke(end);
            isFirst = false;
        }

        public void Run(MessageMock from, MessageMock to)
        {
            start = from;
            end = to;

            // Can skip
            if (isFirst || (Vector3.Distance(from.position, to.position) < minPositionDelta && receiver.IncomingMessages.Count > 0))
                OnDisable();
            else
                enabled = true;
        }
    }
}
