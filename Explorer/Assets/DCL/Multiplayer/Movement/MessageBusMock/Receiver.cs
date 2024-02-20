using Castle.Core.Internal;
using Cysharp.Threading.Tasks;
using DCL.Multiplayer.Movement.MessageBusMock.Movement;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
using Random = UnityEngine.Random;

namespace DCL.Multiplayer.Movement.MessageBusMock
{
    public class Receiver : MonoBehaviour
    {
        public readonly Queue<MessageMock> IncomingMessages = new ();
        private readonly List<MessageMock> passedMessages = new ();

        public MessageBus messageBus;
        public GameObject receivedMark;
        public GameObject passedMark;

        [Header("DEBUG")]
        public int Incoming;
        public int Passed;
        public bool isBlending;

        [Header("BLENDING")]
        public bool useBlend;
        public float maxBlendSpeed = 30f;
        public float maxBlendExtraTime = 0.33f;
        public float blendDuration;
        public Vector3 blendVelocity;
        public MessageMock blendTargetPoint;

        [Space]
        public float blendExtra;

        private Interpolation interpolation;
        private Extrapolation extrapolation;

        private void Awake()
        {
            extrapolation = GetComponent<Extrapolation>();
            interpolation = GetComponent<Interpolation>();

            extrapolation.enabled = false;
            interpolation.enabled = false;

            interpolation.PointPassed += AddToPassed;
        }

        private void Start()
        {
            messageBus.MessageSent += newMessage =>
                UniTask.Delay(TimeSpan.FromSeconds(messageBus.Latency + (messageBus.Latency * Random.Range(0, messageBus.LatencyJitter)) + (messageBus.PackageSentRate * Random.Range(0, messageBus.PackagesJitter))))
                       .ContinueWith(() =>
                        {
                            IncomingMessages.Enqueue(newMessage);
                            PutMark(newMessage, receivedMark, 0.11f);
                        })
                       .Forget();
        }

        private void Update()
        {
            Incoming = IncomingMessages.Count;
            Passed = passedMessages.Count;

            if (interpolation.enabled) return;

            if (IncomingMessages.Count > 0)
            {
                var endPoint = IncomingMessages.Dequeue();

                if (extrapolation.enabled)
                {
                    extrapolation.enabled = false;

                    passedMessages.Add(new MessageMock
                    {
                        timestamp = passedMessages[^1].timestamp + extrapolation.Time,
                        position = transform.position,
                        velocity = extrapolation.Velocity,
                    });
                }

                var startPoint = passedMessages.Count > 0 ? passedMessages[^1] : null;

                interpolation.Run(startPoint, endPoint);
            }
            else if (passedMessages.Count > 1 && !extrapolation.enabled && !passedMessages.IsNullOrEmpty())
            {
                extrapolation.Run(passedMessages[^1]);
            }

            // if (incomingMessages.Count == 0)
            // {
            //     if (passedMessages.Count > 1 && useExtrapolation && !isExtrapolating && !passedMessages.IsNullOrEmpty())
            //         StartCoroutine(Extrapolate());
            // }
            // else
            // {
            //     // Next interpolation point
            //     endPoint = incomingMessages.Dequeue();
            //
            //     if (isFirst)
            //     {
            //         isFirst = false;
            //         transform.position = endPoint.position;
            //         AddToPassed(endPoint);
            //         return;
            //     }
            //
            //     // Stop extrapolation when message arrives
            //     if (isExtrapolating)
            //     {
            //         if (endPoint.timestamp > passedMessages[^1].timestamp + extDuration)
            //         {
            //             isExtrapolating = false;
            //
            //             AddToPassed(new MessageMock
            //             {
            //                 timestamp = passedMessages[^1].timestamp + extDuration,
            //                 position = transform.position,
            //                 velocity = extVelocity,
            //             });
            //
            //             if (useBlend)
            //             {
            //                 blendTargetPoint = endPoint;
            //                 StartCoroutine(Blend(passedMessages[^1], blendTargetPoint));
            //                 return;
            //             }
            //         }
            //         else if (useBlend && endPoint.timestamp > passedMessages[^1].timestamp)
            //         {
            //             isExtrapolating = false;
            //
            //             float currentTimestamp = passedMessages[^1].timestamp + extDuration;
            //
            //             AddToPassed(new MessageMock
            //             {
            //                 timestamp = currentTimestamp,
            //                 position = transform.position,
            //                 velocity = extVelocity,
            //             });
            //
            //             float deltaT = currentTimestamp - endPoint.timestamp;
            //
            //             blendTargetPoint = new MessageMock
            //             {
            //                 timestamp = currentTimestamp + 0.001f,
            //                 position = endPoint.position + (endPoint.velocity * deltaT),
            //                 velocity = endPoint.velocity,
            //             };
            //
            //             StartCoroutine(Blend(passedMessages[^1], blendTargetPoint));
            //             return;
            //         }
            //         else { return; }
            //     }
            //
            //     if (isBlending && endPoint.timestamp > blendTargetPoint.timestamp)
            //     {
            //         StopAllCoroutines();
            //
            //         AddToPassed(new MessageMock
            //         {
            //             timestamp = passedMessages[^1].timestamp + blendDuration,
            //             position = transform.position,
            //             velocity = blendVelocity,
            //         });
            //
            //         blendTargetPoint = endPoint;
            //
            //         StartCoroutine(Blend(passedMessages[^1], blendTargetPoint));
            //         return;
            //     }
            //
            //     if (endPoint.timestamp > passedMessages[^1].timestamp)
            //         Interpolate(start: passedMessages[^1], endPoint);
            // }
        }

        private void AddToPassed(MessageMock message)
        {
            passedMessages.Add(message);
            PutMark(message, passedMark, 0.2f);
        }

        private static void PutMark(MessageMock newMessage, GameObject mark, float f)
        {
            GameObject markPoint = Instantiate(mark);
            markPoint.transform.position = newMessage.position + (Vector3.up * f);
            markPoint.SetActive(true);
        }

        // private IEnumerator Blend(MessageMock local, MessageMock remote)
        // {
        //     isBlending = true;
        //
        //     float positionDiff = Vector3.Distance(local.position, remote.position);
        //
        //     if (positionDiff < minPositionDelta)
        //     {
        //         AddToPassed(remote);
        //         isBlending = false;
        //         yield break;
        //     }
        //
        //     float timeDiff = remote.timestamp - local.timestamp;
        //     Vector3 remoteOldPosition = remote.position - (remote.velocity * timeDiff);
        //
        //     var avarageMessageSentRate = 0f;
        //
        //     if (passedMessages.Count > 4)
        //     {
        //         avarageMessageSentRate += passedMessages[^2].timestamp - passedMessages[^3].timestamp;
        //         avarageMessageSentRate += passedMessages[^3].timestamp - passedMessages[^4].timestamp;
        //         avarageMessageSentRate += passedMessages[^4].timestamp - passedMessages[^5].timestamp;
        //
        //         avarageMessageSentRate /= 3;
        //     }
        //     else if (passedMessages.Count > 3)
        //     {
        //         avarageMessageSentRate += passedMessages[^2].timestamp - passedMessages[^3].timestamp;
        //         avarageMessageSentRate += passedMessages[^3].timestamp - passedMessages[^4].timestamp;
        //
        //         avarageMessageSentRate /= 2;
        //     }
        //     else if (passedMessages.Count > 2) { avarageMessageSentRate += passedMessages[^2].timestamp - passedMessages[^3].timestamp; }
        //
        //     blendExtra = Mathf.Clamp(avarageMessageSentRate - timeDiff, 0, maxBlendExtraTime);
        //
        //     // Debug.Log($"{blendExtra} | {timeDiff} | {avarageMessageSentRate}  |  {avarageMessageSentRate - timeDiff}");
        //
        //     float totalDuration = timeDiff + blendExtra;
        //
        //     var slowDownFactor = 1f;
        //     float speed = positionDiff / totalDuration;
        //
        //     if (speed > maxBlendSpeed)
        //     {
        //         float desiredDuration = positionDiff / maxBlendSpeed;
        //         slowDownFactor = desiredDuration / totalDuration;
        //     }
        //
        //     var t = 0f;
        //     blendDuration = 0f;
        //
        //     while (t < totalDuration)
        //     {
        //         blendDuration += Time.deltaTime;
        //
        //         t += Time.deltaTime / slowDownFactor;
        //
        //         float lerpValue = t / totalDuration;
        //
        //         // Interpolate velocity
        //         blendVelocity = local.velocity + ((remote.velocity - local.velocity) * lerpValue);
        //
        //         // Calculate the position at time t
        //         Vector3 projectedLocal = local.position + (blendVelocity * t);
        //         Vector3 projectedRemote = remoteOldPosition + (remote.velocity * t);
        //
        //         // Apply the interpolated position
        //         transform.position = projectedLocal + ((projectedRemote - projectedLocal) * lerpValue);
        //
        //         yield return null;
        //     }
        //
        //     AddToPassed(remote);
        //
        //     if (blendExtra > 0f)
        //     {
        //         AddToPassed(new MessageMock
        //         {
        //             timestamp = remote.timestamp + blendExtra,
        //             position = transform.position,
        //             velocity = remote.velocity,
        //         });
        //     }
        //
        //     isBlending = false;
        // }
        //

    }
}
