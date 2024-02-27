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

        private float time;

        private MessageMock start;
        private MessageMock end;
        private float totalDuration;

        private Func<MessageMock, MessageMock, float, float, Vector3> interpolationFunc;
        private Func<MessageMock, MessageMock, float, float, Vector3> blendFunc;

        private bool isBlend;
        private float slowDownFactor;
        private int speedUpFactor;
        private float MaxSpeed;

        public InterpolationComponent(Transform transform)
        {
            Transform = transform;

            Enabled = false;
            isBlend = false;

            interpolationFunc = null;
            blendFunc = null;

            start = null;
            end = null;
            time = 0f;
            totalDuration = 0f;
            slowDownFactor = 1f;
            speedUpFactor = 0;
            MaxSpeed = 20f;
        }

        public MessageMock Update(float deltaTime)
        {
            time += deltaTime / slowDownFactor;

            if (time >= totalDuration) return Disable();

            Transform.position = isBlend ? blendFunc(start, end, time, totalDuration) : interpolationFunc(start, end, time, totalDuration);
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

        private void Enable(int inboxMessages, InterpolationType intType, InterpolationType blendType)
        {
            time = 0f;
            slowDownFactor = 1f;
            totalDuration = end.timestamp - start.timestamp;

            if (isBlend)
            {
                float positionDiff = Vector3.Distance(start.position, end.position);
                float speed = positionDiff / totalDuration;

                if (speed > MaxSpeed)
                {
                    float desiredDuration = positionDiff / MaxSpeed;
                    slowDownFactor = desiredDuration / totalDuration;
                }
            }
            else
            {
                float correctionTime = (speedUpFactor + inboxMessages) * Time.smoothDeltaTime;
                totalDuration = Mathf.Max(totalDuration - correctionTime, totalDuration / 4f);
            }

            interpolationFunc = GetInterpolationFunc(intType);
            blendFunc = GetInterpolationFunc(blendType);

            Enabled = true;
        }

        public void Run(MessageMock from, MessageMock to, int inboxMessages, InterpolationType type = InterpolationType.Linear, InterpolationType blendType = InterpolationType.Linear, bool isBlend = false)
        {
            if (from?.timestamp >= to.timestamp) return;

            start = from;
            end = to;

            this.isBlend = isBlend;

            Enable(inboxMessages, type, blendType);
        }

        private MessageMock Disable()
        {
            Transform.position = end.position;
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
