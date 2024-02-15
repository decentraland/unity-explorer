using Castle.Core.Internal;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace DCL.Multiplayer.Movement.MessageBusMock
{
    public enum InterpolationType
    {
        Linear,
        Hermite,
        Bezier,
        VelocityBlending,
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
        [Header("BLENDING")]
        public bool isBlending;
        public float blendExtra;
        public bool useBlend;

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

            if (isInterpolating || isBlending) return;

            if (incomingMessages.Count > 0)
            {
                // Next interpolation point
                endPoint = incomingMessages.Dequeue();

                if (isFirst)
                {
                    isFirst = false;
                    transform.position = endPoint.position;
                    passedMessages.Add(endPoint);
                    return;
                }

                // Stop extrapolation when message arrives
                if (isExtrapolating)
                {
                    isExtrapolating = false;
                    StopAllCoroutines();

                    passedMessages.Add(new MessageMock
                    {
                        timestamp = passedMessages[^1].timestamp + extDuration,
                        position = transform.position,
                        velocity = extVelocity,
                    });

                    if (useBlend)
                    {
                        StartCoroutine(Blend(passedMessages[^1], endPoint));
                        return;
                    }
                }
            }
            else if (!passedMessages.IsNullOrEmpty() && !isExtrapolating && passedMessages[^1].velocity.sqrMagnitude > minSpeed)
            {
                StartCoroutine(Extrapolate());
                return;
            }
            else return;

            Interpolate(start: passedMessages[^1], endPoint);
        }

        private IEnumerator Blend(MessageMock local, MessageMock remote)
        {
            isBlending = true;

            if (Vector3.Distance(local.position, remote.position) < minPositionDelta)
            {
                passedMessages.Add(remote);
                isBlending = false;
                yield break;
            }

            float timeDiff = remote.timestamp - local.timestamp;

            Vector3 remoteOldPosition = remote.position - (remote.velocity * timeDiff);

            var t = 0f;
            var totalDuration = timeDiff + blendExtra;

            while (t < totalDuration)
            {
                t += UnityEngine.Time.deltaTime;

                var lerpValue = t / totalDuration;

                // Interpolate velocity
                Vector3 lerpedVelocity = local.velocity + ((remote.velocity - local.velocity) * lerpValue);

                // Calculate the position at time t
                Vector3 P_t = local.position + (lerpedVelocity * t);
                Vector3 P_t_n = remoteOldPosition + (remote.velocity * t);

                // Apply the interpolated position
                transform.position = P_t + ((P_t_n - P_t) * lerpValue);

                yield return null;
            }

            // transform.position = remote.position;
            passedMessages.Add(remote);
            passedMessages.Add(new MessageMock
            {
                timestamp = remote.timestamp + blendExtra,
                position = transform.position,
                velocity = remote.velocity,
            });

            isBlending = false;
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

        private void Interpolate(MessageMock start, MessageMock end)
        {
            StartCoroutine(Move(start, end, interpolation: interpolationType switch
                                                           {
                                                               InterpolationType.Linear => LinearInterpolation,
                                                               InterpolationType.Hermite => HermiteInterpolation,
                                                               InterpolationType.Bezier => BezierInterpolation,
                                                               InterpolationType.VelocityBlending => VelocityBlendingInterpolation,
                                                               _ => LinearInterpolation,
                                                           }));
        }

        private IEnumerator Move(MessageMock start, MessageMock end, Func<MessageMock, MessageMock, float, float, Vector3> interpolation)
        {
            isInterpolating = true;

            if (Vector3.Distance(start.position, endPoint.position) > minPositionDelta)
            {
                var t = 0f;
                float totalDuration = end.timestamp - start.timestamp;

                while (t < totalDuration)
                {
                    t += UnityEngine.Time.deltaTime;
                    transform.position = interpolation(start, end, t, totalDuration);
                    yield return null;
                }
            }

            transform.position = end.position;
            passedMessages.Add(end);

            isInterpolating = false;
        }

        private static Vector3 LinearInterpolation(MessageMock start, MessageMock end, float t, float totalDuration) =>
            Vector3.Lerp(start.position, end.position, t / totalDuration);

        private static Vector3 VelocityBlendingInterpolation(MessageMock start, MessageMock end, float t, float totalDuration)
        {
            Vector3 fakeOldPosition = end.position - (end.velocity * totalDuration);

            float lerpValue = t / totalDuration;

            Vector3 lerpedVelocity = start.velocity + ((end.velocity - start.velocity) * lerpValue); // Interpolated velocity

            // Calculate the position at time t
            Vector3 P_t = start.position + (lerpedVelocity * t);
            Vector3 P_t_n = fakeOldPosition + (end.velocity * t);

            return P_t + ((P_t_n - P_t) * lerpValue); // interpolate positions
        }

        /// Cubic Hermite spline interpolation
        private static Vector3 HermiteInterpolation(MessageMock start, MessageMock end, float t, float totalDuration)
        {
            float lerpValue = t / totalDuration;

            float t2 = lerpValue * lerpValue;
            float t3 = t2 * lerpValue;

            float h2 = (-2 * t3) + (3 * t2); // Hermite basis function h_01 (for end position)
            float h1 = -h2 + 1; // Hermite basis function h_00 (for start position)

            float h3 = t3 - (2 * t2) + lerpValue; // Hermite basis function h_10 (for start velocity)
            float h4 = t3 - t2; // Hermite basis function h_11 (for end velocity)

            // note: (start.velocity * timeDif) and (end.velocity * timeDif) can be cached
            return (h1 * start.position) + (h2 * end.position) + (start.velocity * (h3 * totalDuration)) + (end.velocity * (h4 * totalDuration));
        }

        /// Cubic Bézier spline interpolation
        private static Vector3 BezierInterpolation(MessageMock start, MessageMock end, float t, float totalDuration)
        {
            float lerpValue = t / totalDuration;

            // Compute the control points based on start and end positions and velocities
            // note: c0 and c1 can be cached
            Vector3 c0 = start.position + (start.velocity * (totalDuration / 3));
            Vector3 c1 = end.position - (end.velocity * (totalDuration / 3));

            float t2 = lerpValue * lerpValue;
            float t3 = t2 * lerpValue;

            float oneMinusT = 1 - lerpValue;
            float oneMinusT2 = oneMinusT * oneMinusT;
            float oneMinusT3 = oneMinusT2 * oneMinusT;

            return (oneMinusT3 * start.position) + (3 * oneMinusT2 * lerpValue * c0) + (3 * oneMinusT * t2 * c1) + (t3 * end.position);
        }
    }
}
