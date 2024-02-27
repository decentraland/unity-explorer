using DCL.Multiplayer.Movement.MessageBusMock;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace DCL.Multiplayer.Movement.ECS
{
    public struct InterpolationComponent
    {
        public bool Enabled;

        public readonly Transform Transform;

        private MessageMock start;
        private MessageMock end;

        private float time;
        private float totalDuration;
        private float slowDownFactor;

        private bool isBlend;

        private MessagePipeSettings settings;

        public InterpolationComponent(Transform transform)
        {
            Transform = transform;
            settings = null;

            Enabled = false;

            start = null;
            end = null;
            time = 0f;
            totalDuration = 0f;

            isBlend = false;
            slowDownFactor = 1f;
        }

        public MessageMock Update(float deltaTime)
        {
            time += deltaTime / slowDownFactor;

            if (time >= totalDuration) return Disable();

            Transform.position = DoTransition(start, end, time, totalDuration, isBlend);
            UpdateRotation();
            return null;
        }

        private void UpdateRotation()
        {
            Vector3 flattenedDiff = end.position - Transform.position;
            flattenedDiff.y = 0;

            if (flattenedDiff != Vector3.zero)
            {
                var lookRotation = Quaternion.LookRotation(flattenedDiff, Vector3.up);
                Transform.rotation = lookRotation;
            }
        }

        private void Enable(int inboxMessages)
        {
            time = 0f;
            slowDownFactor = 1f;
            totalDuration = end.timestamp - start.timestamp;

            if (isBlend)
            {
                float positionDiff = Vector3.Distance(start.position, end.position);
                float speed = positionDiff / totalDuration;

                if (speed > settings.MaxBlendSpeed)
                {
                    float desiredDuration = positionDiff / settings.MaxBlendSpeed;
                    slowDownFactor = desiredDuration / totalDuration;
                }
            }
            else
            {
                float correctionTime = (settings.SpeedUpFactor + inboxMessages) * Time.smoothDeltaTime;
                totalDuration = Mathf.Max(totalDuration - correctionTime, totalDuration / 4f);
            }

            Enabled = true;
        }

        public void Run(MessageMock from, MessageMock to, int inboxMessages, MessagePipeSettings settings, bool isBlend = false)
        {
            this.settings = settings;

            if (from?.timestamp >= to.timestamp) return;

            start = from;
            end = to;

            this.isBlend = isBlend;

            Enable(inboxMessages);
        }

        private MessageMock Disable()
        {
            Transform.position = end.position;
            Enabled = false;

            return end;
        }

        private Vector3 DoTransition(MessageMock start, MessageMock end, float time, float totalDuration, bool isBlend)
        {
            return (isBlend ? settings.BlendType : settings.InterpolationType) switch
                   {
                       InterpolationType.Linear => Interpolate.Linear(start, end, time, totalDuration),
                       InterpolationType.PositionBlending => Interpolate.ProjectivePositionBlending(start, end, time, totalDuration),
                       InterpolationType.VelocityBlending => Interpolate.ProjectiveVelocityBlending(start, end, time, totalDuration),
                       InterpolationType.Bezier => Interpolate.Bezier(start, end, time, totalDuration),
                       InterpolationType.Hermite => Interpolate.Hermite(start, end, time, totalDuration),
                       _ => Interpolate.Linear(start, end, time, totalDuration),
                   };
        }
    }
}
