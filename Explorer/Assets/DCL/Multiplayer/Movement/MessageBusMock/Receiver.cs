using Cysharp.Threading.Tasks;
using DCL.Multiplayer.Movement.MessageBusMock.Movement;
using NSubstitute.Exceptions;
using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;

namespace DCL.Multiplayer.Movement.MessageBusMock
{
    public class Receiver : MonoBehaviour
    {
        private const float MIN_POSITION_DELTA = 0.01f;

        private const float MIN_SPEED = 0.01f;

        public readonly Queue<MessageMock> IncomingMessages = new ();
        private readonly List<MessageMock> passedMessages = new ();

        public float MaxPosition = 10f;

        public int Incoming;
        public int Passed;

        [Header("INTERPOLATION")]
        public InterpolationType interpolationType;

        [Space]
        public MessageBus messageBus;
        public GameObject receivedMark;
        public GameObject passedMark;

        private Interpolation interpolation;
        private Extrapolation extrapolation;

        private void Awake()
        {
            interpolation = new Interpolation(transform);
            extrapolation = new Extrapolation(transform);
        }

        // Network simulation
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

            if (interpolation.IsRunning)
            {
                interpolation.Update(Time.deltaTime)?.AddToPassed(passedMessages, passedMark);
                return;
            }

            if (passedMessages.Count == 0 && IncomingMessages.Count == 0) return;
            if (passedMessages.Count == 0 && IncomingMessages.Count == 1)
            {
                Teleport(IncomingMessages.Dequeue());
                return;
            }

            if (passedMessages.Count == 1 && IncomingMessages.Count == 0) return;
            if (passedMessages.Count == 1 && IncomingMessages.Count == 1)
            {
                StartInterpolation(IncomingMessages.Dequeue());
                return;
            }



            if (HasNoMessages())
            {
                if (extrapolation.IsRunning)
                    extrapolation.Update(Time.deltaTime);
                else if (passedMessages[^1].velocity.sqrMagnitude > MIN_SPEED)
                {
                    extrapolation.Run(passedMessages[^1], messageBus.PackageSentRate, MIN_SPEED);
                    extrapolation.Update(Time.deltaTime);
                }

                return;
            }

            if (HasNewMessages())
            {
                MessageMock remote = IncomingMessages.Dequeue();
                if (remote.timestamp < passedMessages[^1].timestamp) return;

                if (!extrapolation.IsRunning)
                    StartInterpolation(remote);
                else
                {
                    MessageMock local = extrapolation.Stop();
                    passedMessages.Add(local);

                    float positionDiff = Vector3.Distance(local.position, remote.position);

                    // if(positionDiff < MIN_POSITION_DELTA || positionDiff > MaxPosition)
                    // {
                    //     // but it falls then into pure extrapolation
                    //     Teleport(remote);
                    //     return;
                    // }

                    float timeDiff = remote.timestamp - local.timestamp;
                    float speed = positionDiff / timeDiff;
                    if (speed < 15)
                    {
                        interpolation.Run(local, remote, interpolationType, IncomingMessages.Count);
                        interpolation.Update(Time.deltaTime)?.AddToPassed(passedMessages, passedMark);
                    }
                    else
                    {
                        passedMessages.Add(remote);

                        MessageMock projectedRemote = new MessageMock
                        {
                            timestamp = remote.timestamp + messageBus.PackageSentRate,
                            position = remote.position + (remote.velocity * messageBus.PackageSentRate),
                            velocity = remote.velocity,
                        };

                        extrapolation.Run(local, projectedRemote, MIN_SPEED);
                        extrapolation.Update(Time.deltaTime);
                    }

                    // ELSE -> Adjust extrapolation!
                }
            }

            //// ---- Extrapolate (with blend inside) ----
            // if(FirstMessageArrived)                teleport()                   <------
            // if(SecondIsNotArrived)                 wait                         <------

            // if(isInterpolating)                    interpolate.Update           <------
            // if(NoNewMessages)                      extrapolate.Start? + Update  <------

            // if(HasNewMessages)
            //   if(PointIsOlder)                     wait                         <------
            //   if(!isExtrapolating)                 interpolate.Start + Update   <------
            //   if(isExtrapolating)
            //       1. if(acceptableToInterpolate(current, end)) interpolation.Start  <------
            //       2. adjust extrapolations: - (calculate start, calculate end)      <------
        }

        private void StartInterpolation(MessageMock to)
        {
            MessageMock from = passedMessages[^1];

            if (Vector3.Distance(from.position, to.position) < MIN_POSITION_DELTA && IncomingMessages.Count > 0)
                Teleport(to);
            else
            {
                interpolation.Run(from, to, interpolationType, IncomingMessages.Count);
                interpolation.Update(Time.deltaTime)?.AddToPassed(passedMessages, passedMark);
            }
        }

        private bool HasNoMessages() =>
            IncomingMessages.Count == 0;

        private bool HasNewMessages() =>
            IncomingMessages.Count != 0;

        private void Teleport(MessageMock to)
        {
            transform.position = to.position;
            AddToPassed(to);
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

    public static class MessageMockExtensions
    {
        public static void AddToPassed(this MessageMock message, List<MessageMock> passedMessages, GameObject passedMark)
        {
            if (message == null) return;

            passedMessages.Add(message);
            PutMark(message, passedMark, 0.2f);
            return;

            void PutMark(MessageMock newMessage, GameObject mark, float f)
            {
                GameObject markPoint = Object.Instantiate(mark);
                markPoint.transform.position = newMessage.position + (Vector3.up * f);
                markPoint.SetActive(true);
            }
        }
    }
}
