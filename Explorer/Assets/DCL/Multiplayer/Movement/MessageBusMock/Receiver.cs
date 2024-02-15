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
                Vector3 projectedLocal = local.position + (lerpedVelocity * t);
                Vector3 projectedRemote = remoteOldPosition + (remote.velocity * t);

                // Apply the interpolated position
                transform.position = projectedLocal + ((projectedRemote - projectedLocal) * lerpValue);

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
                                                               InterpolationType.Linear => Interpolation.Linear,
                                                               InterpolationType.Hermite => Interpolation.Hermite,
                                                               InterpolationType.Bezier => Interpolation.Bezier,
                                                               InterpolationType.VelocityBlending => Interpolation.VelocityBlending,
                                                               _ => Interpolation.Linear,
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


    }
}
