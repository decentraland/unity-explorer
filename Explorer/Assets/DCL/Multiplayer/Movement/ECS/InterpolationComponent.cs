using DCL.Multiplayer.Movement.MessageBusMock;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace DCL.Multiplayer.Movement.ECS
{
    public struct InterpolationComponent
    {
        private readonly List<MessageMock> passedMessages;
        private readonly Transform transform;

        private bool isFirst;

        public bool isInterpolating;

        private MessageMock from;
        private MessageMock to;
        private float t;

        private Func<MessageMock, MessageMock, float, float, Vector3> interpolationFunc;

        public InterpolationComponent(Transform transform)
        {
            this.transform = transform;

            passedMessages = new List<MessageMock>();

            isFirst = true;
            isInterpolating = false;

            interpolationFunc = null;
            from = null;
            to = null;
            t = 0f;
        }

        public void Update(float deltaTime)
        {
            if (!isInterpolating) return;

            if (t < to.timestamp - from.timestamp)
            {
                t += deltaTime;
                transform.position = interpolationFunc(from, to, t, to.timestamp - from.timestamp);
            }
            else
            {
                transform.position = to.position;
                passedMessages.Add(to);

                isInterpolating = false;
            }
        }

        public void StartInterpolate(MessageMock end, InterpolationType type = InterpolationType.Linear)
        {
            if (isFirst)
            {
                transform.position = end.position;
                passedMessages.Add(end);
                isFirst = false;
            }
            else
            {
                isInterpolating = true;
                t = 0f;

                from = passedMessages[^1];
                to = end;
            }

            interpolationFunc = SetInterpolationFunc(type);
        }

        private static Func<MessageMock, MessageMock, float, float, Vector3> SetInterpolationFunc(InterpolationType type)
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
