using System;
using UnityEngine;

namespace DCL.Multiplayer.Movement.MessageBusMock
{
    public class Interpolation : MonoBehaviour
    {
        public Receiver receiver;

        public InterpolationType interpolationType;
        public float minPositionDelta = 0.1f;

        private MessageMock start;
        private MessageMock end;

        private float t;
        private float totalDuration;

        private Func<MessageMock, MessageMock, float, float, Vector3> interpolation;

        private bool isFirst = true;

        public event Action<MessageMock> PointPassed;

        private void Update()
        {
            if (t < totalDuration)
            {
                t += Time.deltaTime;
                transform.position = interpolation(start, end, t, totalDuration);
            }
            else
                enabled = false;
        }

        private void OnEnable()
        {
            t = 0f;

            float timeDiff = end.timestamp - start.timestamp;
            float correctionTime = receiver.IncomingMessages.Count * Time.smoothDeltaTime;
            totalDuration = Mathf.Max(timeDiff - correctionTime, timeDiff / 3f);

            interpolation = interpolationType switch
                            {
                                InterpolationType.Linear => MessageBusMock.Interpolate.Linear,
                                InterpolationType.Hermite => MessageBusMock.Interpolate.Hermite,
                                InterpolationType.Bezier => MessageBusMock.Interpolate.Bezier,
                                InterpolationType.VelocityBlending => MessageBusMock.Interpolate.ProjectiveVelocityBlending,
                                _ => MessageBusMock.Interpolate.Linear,
                            };
        }

        private void OnDisable()
        {
            transform.position = end.position;
            PointPassed?.Invoke(end);
            isFirst = false;
        }

        public void Interpolate(MessageMock from, MessageMock to)
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
