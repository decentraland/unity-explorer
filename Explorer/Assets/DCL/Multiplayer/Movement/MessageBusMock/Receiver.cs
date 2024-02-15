using Castle.Core.Internal;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace DCL.Multiplayer.Movement.MessageBusMock
{
    public enum InterpolationType
    {
        Linear,
        Hermite,
        Bezier,
    }

    public class Receiver : MonoBehaviour
    {
        private readonly Queue<MessageMock> incomingMessages = new ();
        private readonly List<MessageMock> passedMessages = new ();

        public MessageBus messageBus;

        [Space]
        [Header("INTERPOLATION")]
        public bool isInterpolating;
        public InterpolationType interpolationType;
        public float minPositionDelta;
        public MessageMock endPoint;

        [Space]
        [Header("EXTRAPOLATION")]
        public bool isExtrapolating;
        public float minSpeed = 0.1f;
        public float linearExtrapolationTime = 0.33f;
        public int dampedExtrapolationSteps = 2;

        public float extDuration;
        public Vector3 extVelocity;

        [Space]
        [Header("DEBUG")]
        public int Incoming;
        public int Processed;

        private bool isFirst = true;

        private void Awake()
        {
            messageBus.MessageSent += newMessage => incomingMessages.Enqueue(newMessage);
        }

        private void Update()
        {
            Incoming = incomingMessages.Count;
            Processed = passedMessages.Count;

            if (isInterpolating) return;

            if (incomingMessages.Count > 0)
            {
                MessageMock newMessage = incomingMessages.Dequeue();

                if (isFirst)
                {
                    isFirst = false;
                    transform.position = newMessage.position;
                    passedMessages.Add(newMessage);
                    return;
                }

                endPoint = newMessage;

                if (isExtrapolating  && passedMessages.Count > 1)
                {
                    StopAllCoroutines();

                    passedMessages.Add(new MessageMock
                    {
                        timestamp = passedMessages[^1].timestamp + extDuration,
                        position = transform.position,
                        velocity = extVelocity,
                    });

                    Debug.Log(endPoint.timestamp - passedMessages[^1].timestamp);
                }

                isExtrapolating = false;
            }
            else if (!passedMessages.IsNullOrEmpty() && !isExtrapolating && passedMessages[^1].velocity.sqrMagnitude > minSpeed)
            {
                StartCoroutine(Extrapolate());
                return;
            }
            else return;

            Interpolate(start: passedMessages[^1], endPoint);
        }

        private IEnumerator Extrapolate()
        {
            isExtrapolating = true;

            extDuration = 0f;
            extVelocity = passedMessages[^1].velocity;

            Vector3 initialVelocity = extVelocity;

            float maxDuration = linearExtrapolationTime * dampedExtrapolationSteps;

            while (isExtrapolating)
            {
                extDuration += UnityEngine.Time.deltaTime;

                if (extDuration > linearExtrapolationTime && extDuration < maxDuration)
                    extVelocity = Vector3.Lerp(initialVelocity, Vector3.zero, extDuration / maxDuration);
                else if (extDuration >= maxDuration)
                    extVelocity = Vector3.zero;

                if (extVelocity.sqrMagnitude > minSpeed)
                    transform.position += extVelocity * UnityEngine.Time.deltaTime;

                yield return null;
            }
        }

        private MessageMock ExtrapolateNextPosition()
        {
            MessageMock lastMessage = passedMessages[^1];
            MessageMock preLastMessage = passedMessages[^2];

            float timeDiff = lastMessage.timestamp - preLastMessage.timestamp;

            return new MessageMock
            {
                timestamp = lastMessage.timestamp + timeDiff, // Delta time to handle network jitter
                position = lastMessage.position + (lastMessage.velocity * timeDiff), // Assuming linear motion (constant velocity) for extrapolation
                velocity = lastMessage.velocity, // Assuming constant velocity
            };
        }

        private void Interpolate(MessageMock start, MessageMock end)
        {
            StartCoroutine(MoveTo(start, end, interpolation: interpolationType switch
                                                             {
                                                                 InterpolationType.Linear => LinearInterpolation,
                                                                 InterpolationType.Hermite => HermiteInterpolation,
                                                                 InterpolationType.Bezier => BezierInterpolation,
                                                                 _ => LinearInterpolation,
                                                             }));
        }

        private IEnumerator MoveTo(MessageMock start, MessageMock end, Func<MessageMock, MessageMock, float, float, Vector3> interpolation)
        {
            isInterpolating = true;

            if (Vector3.Distance(start.position, endPoint.position) > minPositionDelta)
            {
                var t = 0f;
                float timeDif = end.timestamp - start.timestamp;

                while (t < timeDif)
                {
                    t += UnityEngine.Time.deltaTime;
                    transform.position = interpolation(start, end, t / timeDif, timeDif);
                    yield return null;
                }
            }

            passedMessages.Add(end);
            isInterpolating = false;
        }

        private static Vector3 LinearInterpolation(MessageMock start, MessageMock end, float t, float _) =>
            Vector3.Lerp(start.position, end.position, t);

        /// Cubic Hermite spline interpolation
        private static Vector3 HermiteInterpolation(MessageMock start, MessageMock end, float t, float timeDif)
        {
            float t2 = t * t;
            float t3 = t2 * t;

            float h2 = (-2 * t3) + (3 * t2); // Hermite basis function h_01 (for end position)
            float h1 = -h2 + 1; // Hermite basis function h_00 (for start position)

            float h3 = t3 - (2 * t2) + t; // Hermite basis function h_10 (for start velocity)
            float h4 = t3 - t2; // Hermite basis function h_11 (for end velocity)

            // note: (start.velocity * timeDif) and (end.velocity * timeDif) can be cached
            return (h1 * start.position) + (h2 * end.position) + (start.velocity * (h3 * timeDif)) + (end.velocity * (h4 * timeDif));
        }

        /// Cubic Bézier spline interpolation
        private static Vector3 BezierInterpolation(MessageMock start, MessageMock end, float t, float timeDif)
        {
            // Compute the control points based on start and end positions and velocities
            // note: c0 and c1 can be cached
            Vector3 c0 = start.position + (start.velocity * (timeDif / 3));
            Vector3 c1 = end.position - (end.velocity * (timeDif / 3));

            float t2 = t * t;
            float t3 = t2 * t;

            float oneMinusT = 1 - t;
            float oneMinusT2 = oneMinusT * oneMinusT;
            float oneMinusT3 = oneMinusT2 * oneMinusT;

            return (oneMinusT3 * start.position) + (3 * oneMinusT2 * t * c0) + (3 * oneMinusT * t2 * c1) + (t3 * end.position);
        }
    }
}
