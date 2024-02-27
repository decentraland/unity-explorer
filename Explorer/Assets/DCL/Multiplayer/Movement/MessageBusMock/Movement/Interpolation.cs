using DCL.Multiplayer.Movement.MessageBusMock.Movement;
using System;
using UnityEngine;

namespace DCL.Multiplayer.Movement.MessageBusMock
{
    public class Interpolation : MonoBehaviour
    {
        public Receiver receiver;

        public InterpolationType interpolationType;
        public InterpolationType blendType;

        public float minPositionDelta = 0.1f;
        public float speedUpFactor = 1f;
        public float teleportDistance = 10f;

        [Space]
        public float Time;
        public float MaxSpeed;

        private MessageMock start;
        private MessageMock end;
        private float totalDuration;
        private float slowDownFactor;

        private bool isBlend;

        private Func<MessageMock, MessageMock, float, float, Vector3> interpolation;
        private Func<MessageMock, MessageMock, float, float, Vector3> blend;

        public event Action<MessageMock> PointPassed;

        private void Update()
        {
            Time += UnityEngine.Time.deltaTime / slowDownFactor;

            if (Time < totalDuration)
                transform.position = isBlend ? blend(start, end, Time, totalDuration) : interpolation(start, end, Time, totalDuration);
            else
                enabled = false;
        }

        private void OnEnable()
        {
            Time = 0f;
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
                float correctionTime = (speedUpFactor + receiver.IncomingMessages.Count) * UnityEngine.Time.smoothDeltaTime;
                totalDuration = Mathf.Max(totalDuration - correctionTime, totalDuration / 4f);
            }

            interpolation = GetInterpolationFunc(interpolationType);
            blend = GetInterpolationFunc(blendType);
        }

        private void OnDisable()
        {
            transform.position = end.position;
            PointPassed?.Invoke(end);
        }

        public void Run(MessageMock from, MessageMock to, bool isBlend = false)
        {
            start = from;
            end = to;
            this.isBlend = isBlend;

            if (start.timestamp > end.timestamp) return;

            enabled = true;
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
