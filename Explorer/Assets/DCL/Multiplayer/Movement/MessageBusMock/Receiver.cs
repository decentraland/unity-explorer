using Castle.Core.Internal;
using Cysharp.Threading.Tasks;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

namespace DCL.Multiplayer.Movement.MessageBusMock
{
    public class Receiver : MonoBehaviour
    {
        private readonly Queue<MessageMock> incomingMessages = new ();
        private readonly List<MessageMock> passedMessages = new ();

        public MessageBus messageBus;
        public GameObject receivedMark;
        public GameObject passedMark;

        [Header("DEBUG")]
        public int Incoming;
        public int Passed;
        public bool isInterpolating;
        public bool isExtrapolating;
        public bool isBlending;

        [Header("INTERPOLATION")]
        public InterpolationType interpolationType;
        public float minPositionDelta;
        [Space]
        public MessageMock endPoint;

        [Header("EXTRAPOLATION")]
        public bool useExtrapolation;
        public float minSpeed = 0.1f;
        public float linearExtrapolationTime = 0.33f;
        public int dampedExtrapolationSteps = 2;
        [Space]
        public float extDuration;
        public Vector3 extVelocity;

        [Header("BLENDING")]
        public bool useBlend;
        public float maxBlendSpeed = 30f;
        public float maxBlendExtraTime = 0.33f;

        [Space]
        public float blendExtra;

        private bool isFirst = true;

        private void Start()
        {
            messageBus.MessageSent += newMessage =>
                UniTask.Delay(TimeSpan.FromSeconds(messageBus.Latency + (messageBus.Latency * Random.Range(0, messageBus.LatencyJitter)) + (messageBus.PackageSentRate * Random.Range(0, messageBus.PackagesJitter))))
                       .ContinueWith(() =>
                        {
                            incomingMessages.Enqueue(newMessage);
                            PutMark(newMessage, receivedMark, 0.11f);
                        })
                       .Forget();
        }

        private void Update()
        {
            Incoming = incomingMessages.Count;
            Passed = passedMessages.Count;

            if (isInterpolating || isBlending) return;

            if (incomingMessages.Count > 0)
            {
                // Next interpolation point
                endPoint = incomingMessages.Dequeue();

                if (isFirst)
                {
                    isFirst = false;
                    transform.position = endPoint.position;
                    AddToPassed(endPoint);
                    return;
                }

                // Stop extrapolation when message arrives
                if (isExtrapolating && endPoint.timestamp > passedMessages[^1].timestamp + extDuration)
                {
                    isExtrapolating = false;
                    StopAllCoroutines();

                    AddToPassed(new MessageMock
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

                if (endPoint.timestamp > passedMessages[^1].timestamp)
                    Interpolate(start: passedMessages[^1], endPoint);
            }
            else if (passedMessages.Count > 1 && useExtrapolation && !isExtrapolating && !passedMessages.IsNullOrEmpty())
                StartCoroutine(Extrapolate());
        }

        private static void PutMark(MessageMock newMessage, GameObject mark, float f)
        {
            GameObject markPoint = Instantiate(mark);
            markPoint.transform.position = newMessage.position + (Vector3.up * f);
            markPoint.SetActive(true);
        }

        private void AddToPassed(MessageMock message)
        {
            passedMessages.Add(message);
            PutMark(message, passedMark, 0.2f);
        }

        private IEnumerator Blend(MessageMock local, MessageMock remote)
        {
            isBlending = true;

            float positionDiff = Vector3.Distance(local.position, remote.position);

            if (positionDiff < minPositionDelta)
            {
                AddToPassed(remote);
                isBlending = false;
                yield break;
            }

            float timeDiff = remote.timestamp - local.timestamp;
            Vector3 remoteOldPosition = remote.position - (remote.velocity * timeDiff);

            var avarageMessageSentRate = 0f;

            if (passedMessages.Count > 4)
            {
                avarageMessageSentRate += passedMessages[^2].timestamp - passedMessages[^3].timestamp;
                avarageMessageSentRate += passedMessages[^3].timestamp - passedMessages[^4].timestamp;
                avarageMessageSentRate += passedMessages[^4].timestamp - passedMessages[^5].timestamp;

                avarageMessageSentRate /= 3;
            }
            else if (passedMessages.Count > 3)
            {
                avarageMessageSentRate += passedMessages[^2].timestamp - passedMessages[^3].timestamp;
                avarageMessageSentRate += passedMessages[^3].timestamp - passedMessages[^4].timestamp;

                avarageMessageSentRate /= 2;
            }
            else if (passedMessages.Count > 2) { avarageMessageSentRate += passedMessages[^2].timestamp - passedMessages[^3].timestamp; }

            blendExtra = Mathf.Clamp(avarageMessageSentRate - timeDiff, 0, maxBlendExtraTime);
            Debug.Log($"{blendExtra} | {timeDiff} | {avarageMessageSentRate}  |  {avarageMessageSentRate - timeDiff}");

            float totalDuration = timeDiff + blendExtra;

            var slowDownFactor = 1f;
            float speed = positionDiff / totalDuration;

            if (speed > maxBlendSpeed)
            {
                float desiredDuration = positionDiff / maxBlendSpeed;
                slowDownFactor = desiredDuration / totalDuration;
            }

            var t = 0f;

            while (t < totalDuration)
            {
                t += Time.deltaTime / slowDownFactor;

                float lerpValue = t / totalDuration;

                // Interpolate velocity
                Vector3 lerpedVelocity = local.velocity + ((remote.velocity - local.velocity) * lerpValue);

                // Calculate the position at time t
                Vector3 projectedLocal = local.position + (lerpedVelocity * t);
                Vector3 projectedRemote = remoteOldPosition + (remote.velocity * t);

                // Apply the interpolated position
                transform.position = projectedLocal + ((projectedRemote - projectedLocal) * lerpValue);

                yield return null;
            }

            AddToPassed(remote);

            if (blendExtra > 0f)
            {
                AddToPassed(new MessageMock
                {
                    timestamp = remote.timestamp + blendExtra,
                    position = transform.position,
                    velocity = remote.velocity,
                });
            }

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
                extDuration += Time.deltaTime;

                // Damp velocity
                if (extDuration > linearExtrapolationTime && extDuration < maxDuration)
                    extVelocity = Vector3.Lerp(initialVelocity, Vector3.zero, extDuration / maxDuration);
                else if (extDuration >= maxDuration && extVelocity != Vector3.zero)
                    extVelocity = Vector3.zero;

                // Apply extrapolation
                if (extVelocity.sqrMagnitude > minSpeed)
                    transform.position += extVelocity * Time.deltaTime;

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
                                                               InterpolationType.VelocityBlending => Interpolation.ProjectiveVelocityBlending,
                                                               _ => Interpolation.Linear,
                                                           }));
        }

        private IEnumerator Move(MessageMock start, MessageMock end, Func<MessageMock, MessageMock, float, float, Vector3> interpolation)
        {
            isInterpolating = true;

            float timeDiff = end.timestamp - start.timestamp;

            float correctionTime = incomingMessages.Count * Time.smoothDeltaTime;
            float totalDuration = Mathf.Max(timeDiff - correctionTime, timeDiff / 3f);

            var t = 0f;

            if (CannotSkip())
            {
                while (t < totalDuration)
                {
                    t += Time.deltaTime;

                    transform.position = interpolation(start, end, t, totalDuration);
                    yield return null;
                }
            }

            transform.position = end.position;
            AddToPassed(end);

            isInterpolating = false;

            // we can skip interpolation between 2 equal positions only if we have more messages in a queue (to not fall down to pure extrapolation approach)
            bool CannotSkip() =>
                incomingMessages.Count == 0 || Vector3.Distance(start.position, endPoint.position) > minPositionDelta;
        }
    }
}
