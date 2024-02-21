using DCL.Multiplayer.Movement.MessageBusMock;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace DCL.Multiplayer.Movement.ECS
{
    public struct InterpolationComponent
    {
        private const float MIN_POSITION_DELTA = 0.1f;

        public bool Enabled;

        public readonly List<MessageMock> PassedMessages;
        private readonly Transform transform;

        private float time;

        private MessageMock start;
        private MessageMock end;
        private float totalDuration;

        private Func<MessageMock, MessageMock, float, float, Vector3> interpolationFunc;
        private bool isFirst;

        public InterpolationComponent(Transform transform)
        {
            this.transform = transform;

            PassedMessages = new List<MessageMock>();

            isFirst = true;
            Enabled = false;

            interpolationFunc = null;
            start = null;
            end = null;
            time = 0f;
            totalDuration = 0f;
        }

        public void Update(float deltaTime)
        {
            if (!Enabled) return;

            time += deltaTime;

            if (time < totalDuration)
                transform.position = interpolationFunc(start, end, time, totalDuration);
            else
                Disable();
        }

        public void Run(MessageMock from, MessageMock to, int inboxMessages, InterpolationType type = InterpolationType.Linear)
        {
            if (from?.timestamp > to.timestamp) return;

            start = from;
            end = to;

            if (isFirst || (Vector3.Distance(from.position, to.position) < MIN_POSITION_DELTA && inboxMessages > 0))
                Disable();
            else
                Enable(inboxMessages, type);
        }

        private void Enable(int inboxMessages, InterpolationType type)
        {
            time = 0f;

            float timeDiff = end.timestamp - start.timestamp;
            float correctionTime = inboxMessages * Time.smoothDeltaTime;
            totalDuration = Mathf.Max(timeDiff - correctionTime, timeDiff / 4f);

            interpolationFunc = GetInterpolationFunc(type);

            Enabled = true;
        }

        private void Disable()
        {
            transform.position = end.position;
            PassedMessages.Add(end);
            isFirst = false;

            Enabled = false;
        }

        private static Func<MessageMock, MessageMock, float, float, Vector3> GetInterpolationFunc(InterpolationType type)
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
