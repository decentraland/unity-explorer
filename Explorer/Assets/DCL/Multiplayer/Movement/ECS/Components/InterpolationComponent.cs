using DCL.Multiplayer.Movement.MessageBusMock;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace DCL.Multiplayer.Movement.ECS
{
    public struct InterpolationComponent
    {
        public readonly List<MessageMock> passedMessages;

        private readonly Transform transform;
        private bool isFirst;

        public bool Enabled;

        private MessageMock start;
        public MessageMock end;

        public float Time;

        private Func<MessageMock, MessageMock, float, float, Vector3> interpolationFunc;
        private float totalDuration;

        public InterpolationComponent(Transform transform)
        {
            this.transform = transform;

            passedMessages = new List<MessageMock>();

            isFirst = true;
            Enabled = false;

            interpolationFunc = null;
            start = null;
            end = null;
            Time = 0f;
            totalDuration = 0f;
        }

        public void Update(float deltaTime)
        {
            if (!Enabled) return;

            if (Time < totalDuration)
            {
                Time += deltaTime;
                transform.position = interpolationFunc(start, end, Time, totalDuration);
            }
            else
            {
                transform.position = end.position;
                passedMessages.Add(end);

                Enabled = false;
            }
        }

        public void Run(MessageMock to, int inboxMessages, InterpolationType type = InterpolationType.Linear)
        {
            if (isFirst)
            {
                transform.position = to.position;
                passedMessages.Add(to);
                isFirst = false;
            }
            else
            {
                Enabled = true;
                Time = 0f;

                start = passedMessages[^1];
                end = to;

                float timeDiff = to.timestamp - start.timestamp;
                float correctionTime = inboxMessages * UnityEngine.Time.smoothDeltaTime;
                totalDuration = Mathf.Max(timeDiff - correctionTime, timeDiff / 4f);
            }

            interpolationFunc = GetInterpolationFunc(type);
        }

        public static Func<MessageMock, MessageMock, float, float, Vector3> GetInterpolationFunc(InterpolationType type)
        {
            return type switch
                   {
                       InterpolationType.Linear => Interpolate.Linear,
                       InterpolationType.VelocityBlending => Interpolate.ProjectiveVelocityBlending,
                       InterpolationType.Hermite => Interpolate.Hermite,
                       InterpolationType.Bezier => Interpolate.Bezier,
                       _ => Interpolate.Linear,
                   };
        }
    }
}
