using System;
using UnityEngine;

namespace DCL.Multiplayer.Movement.MessageBusMock.Movement
{
    public class Interpolation
    {
        private readonly Transform transform;

        public bool IsRunning;

        private Func<MessageMock, MessageMock, float, float, Vector3> interpolation;

        private MessageMock start;
        private MessageMock end;
        private float time;
        private float totalDuration;

        public Interpolation(Transform transform)
        {
            this.transform = transform;
        }

        public void Run(MessageMock from, MessageMock to, InterpolationType interpolationType, int inboxCount)
        {
            start = from;
            end = to;

            interpolation = Interpolate.GetInterpolationFunc(interpolationType);

            time = 0f;

            float timeDiff = end.timestamp - start.timestamp;
            float speedUpTime = inboxCount * Time.smoothDeltaTime; // time correction
            totalDuration = Mathf.Max(timeDiff - speedUpTime, timeDiff / 4f);

            IsRunning = true;
        }

        public MessageMock Update(float deltaTime)
        {
            time += deltaTime;

            if (time < totalDuration)
                transform.position = interpolation(start, end, time, totalDuration);
            else
            {
                IsRunning = false;
                transform.position = end.position;
                return end;
            }

            return null;
        }

    }
}
