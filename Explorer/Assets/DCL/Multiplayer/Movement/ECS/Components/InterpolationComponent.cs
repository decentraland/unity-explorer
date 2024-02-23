using DCL.Multiplayer.Movement.MessageBusMock;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace DCL.Multiplayer.Movement.ECS
{
    public struct InterpolationComponent
    {
        private const float MIN_POSITION_DELTA = 0.1f;
        private const float FIRST_DURATION = 0.5f;

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

        public MessageMock Update(float deltaTime)
        {
            time += deltaTime;

            if (time < totalDuration)
            {
                transform.position = interpolationFunc(start, end, time, totalDuration);
                UpdateRotation();
                return null;
            }
            else return Disable();
        }

        private void UpdateRotation()
        {
            Vector3 flattenedDiff = end.position - transform.position;
            flattenedDiff.y = 0;

            if (flattenedDiff != Vector3.zero)
            {
                Quaternion lookRotation = Quaternion.LookRotation(flattenedDiff, Vector3.up);
                transform.rotation = lookRotation;
            }
        }

        public void Run(MessageMock from, MessageMock to, int inboxMessages, InterpolationType type = InterpolationType.Linear, bool isBlend = false)
        {
            if (from?.timestamp >= to.timestamp) return;

            start = isFirst
                ? new MessageMock { position = transform.position, velocity = Vector3.zero, timestamp = 0f }
                : from;

            end = to;

            if (Vector3.Distance(start!.position, end.position) < MIN_POSITION_DELTA && inboxMessages > 0)
                Disable();
            else
                Enable(inboxMessages, type);
        }

        private void Enable(int inboxMessages, InterpolationType type)
        {
            time = 0f;

            float timeDiff = end.timestamp - start.timestamp;
            float correctionTime = inboxMessages * Time.smoothDeltaTime;

            // TODO: make clamping based on maxSpeed (or as function of start/end.speed) instead of current approach?
            totalDuration = isFirst
                ? FIRST_DURATION
                : Mathf.Max(timeDiff - correctionTime, timeDiff / 4f);

            interpolationFunc = GetInterpolationFunc(type);

            Enabled = true;
        }

        private MessageMock Disable()
        {
            transform.position = end.position;
            PassedMessages.Add(end);
            isFirst = false;

            Enabled = false;

            return end;
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
