using Cysharp.Threading.Tasks;
using DCL.Multiplayer.Movement.MessageBusMock.Movement;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace DCL.Multiplayer.Movement.MessageBusMock
{
    public class Receiver : MonoBehaviour
    {
        public readonly Queue<MessageMock> IncomingMessages = new ();
        private readonly List<MessageMock> passedMessages = new ();

        public int Incoming;
        public int Passed;

        [Space]
        public MessageBus messageBus;
        public GameObject receivedMark;
        public GameObject passedMark;

        public bool UseBlendInterpolation;

        private Interpolation interpolation;
        private Extrapolation extrapolation;
        private Blend blend;

        private void Awake()
        {
            extrapolation = GetComponent<Extrapolation>();
            interpolation = GetComponent<Interpolation>();
            blend = GetComponent<Blend>();

            extrapolation.enabled = false;
            interpolation.enabled = false;
            blend.enabled = false;

            interpolation.PointPassed += AddToPassed;
            blend.PointPassed += AddToPassed;

            messageBus.MessageSent += newMessage =>
                UniTask.Delay(TimeSpan.FromSeconds(messageBus.Latency))
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

            if (interpolation.enabled || blend.enabled) return;

            if (IncomingMessages.Count == 0 && !blend.enabled && !extrapolation.enabled && passedMessages.Count > 1)
            {
                extrapolation.Run(passedMessages[^1]);
                return;
            }

            if (IncomingMessages.Count > 0)
            {
                MessageMock remote = IncomingMessages.Dequeue();

                if (extrapolation.enabled)
                {
                    if (remote.timestamp < extrapolation.start.timestamp + extrapolation.Time)
                        return;

                    MessageMock local = extrapolation.Stop();
                    AddToPassed(local);

                    if (UseBlendInterpolation)
                    {
                        blend.Run(local, remote);
                        return;
                    }

                    // - Adjust for far away (Teleport) and slowed down (max speed) interpolations
                    // - Redefine (project) remote point and make such interpolation interaptable
                }

                if (passedMessages.Count == 0
                    || Vector3.Distance(passedMessages[^1].position, remote.position) < interpolation.minPositionDelta
                    || Vector3.Distance(passedMessages[^1].position, remote.position) > interpolation.teleportDistance)
                    Teleport(remote);
                else
                    interpolation.Run(from: passedMessages[^1], to: remote);
            }

            // {
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
            // }
        }

        private void Teleport(MessageMock to)
        {
            transform.position = to.position;
            passedMessages.Clear();
            passedMessages.Add(to);
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
    }
}
