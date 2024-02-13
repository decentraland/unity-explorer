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
    }

    public class Receiver : MonoBehaviour
    {
        [SerializeField] private MessageBus messageBus;
        [SerializeField] private float minDelta;
        [SerializeField] private InterpolationType interpolationType;

        // private readonly Queue<MessageMock> receivedMessages = new ();

        private readonly List<MessageMock> messageHistory = new ();

        private bool isLerping;

        private Func<MessageMock, MessageMock, float, float, Vector3> interpolation;

        private void Awake()
        {
            messageBus.MessageSent += HandleNewMessage;
        }

        private void Update()
        {
            if (isLerping || messageHistory.Count < 2) return;

            interpolation = interpolationType switch
                            {
                                InterpolationType.Linear => LinearInterpolation,
                                InterpolationType.Hermite => HermiteInterpolation,
                                InterpolationType.Bezier => BezierInterpolation,
                            };

            MessageMock startPoint = messageHistory[^2];
            MessageMock endPoint = messageHistory[^1];

            if (Vector3.Distance(startPoint.position, endPoint.position) > minDelta)
                StartCoroutine(MoveTo(startPoint, endPoint, interpolation));
        }

        private void HandleNewMessage(MessageMock newMessage)
        {
            if (messageHistory.Count == 0)
            {
                transform.position = newMessage.position;
            }
            else if (messageHistory.Count == 1)
            {
                var lastMessage = messageHistory[^1];

                lastMessage.velocity = CalculateDiff(lastMessage.position, lastMessage.timestamp, newMessage.position, newMessage.timestamp);

                // suppose velocity didn't change
                newMessage.velocity = lastMessage.velocity;
            }
            else
            {
                var lastMessage = messageHistory[^1];
                var preLastMessage = messageHistory[^2];

                lastMessage.velocity = CalculateDiff(lastMessage.position, lastMessage.timestamp, newMessage.position, newMessage.timestamp);

                preLastMessage.acceleration = CalculateDiff(preLastMessage.velocity, preLastMessage.timestamp, lastMessage.velocity, lastMessage.timestamp);

                // suppose acceleration didn't change
                lastMessage.acceleration = preLastMessage.acceleration;
                newMessage.velocity = lastMessage.velocity + lastMessage.acceleration * (newMessage.timestamp - lastMessage.timestamp);
            }

            messageHistory.Add(newMessage);
        }

        private static Vector3 CalculateDiff(Vector3 start, float startT, Vector3 end, float endT) =>
            (end - start) / (endT - startT);

        // private (MessageMock, MessageMock) GetInterpolationPoints()
        // {
        //     MessageMock startPoint = null;
        //     MessageMock endPoint = null;
        //
        //     if (receivedMessages.Count > 1)
        //     {
        //         startPoint = receivedMessages.Dequeue();
        //         endPoint = receivedMessages.Peek();
        //     }
        //
        //     return (startPoint, endPoint);
        // }

        private IEnumerator MoveTo(MessageMock start, MessageMock end, Func<MessageMock, MessageMock, float, float, Vector3> interpolation)
        {
            isLerping = true;

            var t = 0f;
            float timeDif = end.timestamp - start.timestamp;

            while (t < timeDif)
            {
                t += UnityEngine.Time.deltaTime;
                transform.position = interpolation(start, end, t / timeDif, timeDif);
                yield return null;
            }

            isLerping = false;
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

        // private void OnMessageReceived2(MessageMock newMessage)
        // {
        //     if (history.Count < 2)
        //     {
        //         transform.position = newMessage.position;
        //         AddToHistory(newMessage);
        //         return;
        //     }
        //
        //     // else
        //     //     receivedMessages.Enqueue(newMessage);
        //
        //     if (isLerping || history.Count < 2) return;
        //
        //     var acceleration1 = (newMessage.velocity - history[^1].velocity) / (newMessage.timestamp - history[^1].timestamp);
        //     var acceleration2 = (history[^1].velocity - history[^2].velocity) / (history[^1].timestamp - history[^2].timestamp);
        //     var jerk = (acceleration1 - acceleration2) / (history[^1].timestamp - history[^2].timestamp);
        //     var acceleration = Vector3.zero;// acceleration1 + (jerk * messageBus.PackageSentRate);
        //
        //     transform.position = newMessage.position;
        //
        //     var endPoint = new MessageMock
        //     {
        //         timestamp = newMessage.timestamp + messageBus.PackageSentRate,
        //         position = newMessage.position + (newMessage.velocity * messageBus.PackageSentRate),
        //         velocity = newMessage.velocity + (acceleration * messageBus.PackageSentRate),
        //     };
        //
        //     StartCoroutine(MoveTo(newMessage, endPoint, interpolation));
        // }
    }
}
