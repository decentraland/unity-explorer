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
        private readonly Queue<MessageMock> incomingMessages = new ();
        private readonly List<MessageMock> processedMessages = new (); // Messages that have finished interpolation

        public MessageBus messageBus;
        public float minDelta;
        public InterpolationType interpolationType;

        [Space]
        [Header("DYNAMIC")]
        public bool isLerping;
        public MessageMock endPoint;

        [Space]
        [Header("DEBUG")]
        public int Incoming;
        public int Processed;


        private bool isFirst = true;
        private int extrapolationCount;

        private void Awake()
        {
            messageBus.MessageSent += newMessage => incomingMessages.Enqueue(newMessage);
        }

        private void Update()
        {
            Incoming = incomingMessages.Count;
            Processed = processedMessages.Count;

            if (isLerping) return;


            if (incomingMessages.Count > 0)
            {
                MessageMock newMessage = incomingMessages.Dequeue();

                if (isFirst)
                {
                    isFirst = false;
                    transform.position = newMessage.position;
                    processedMessages.Add(newMessage);
                    return;
                }

                endPoint = newMessage;
                extrapolationCount = 0;
            }
            else if (incomingMessages.Count == 0 && processedMessages.Count > 2 && extrapolationCount == 0)
            {
                endPoint = ExtrapolateNextPosition();
            }
            else return;

            MessageMock startPoint = processedMessages[^1];

            if (Vector3.Distance(startPoint.position, endPoint.position) > minDelta)
            {
                StartCoroutine(MoveTo(startPoint, endPoint, GetInterpolation()));
            }
            else
            {
                processedMessages.Add(endPoint);
            }
        }

        private MessageMock ExtrapolateNextPosition()
        {
            extrapolationCount++;

            MessageMock lastMessage = processedMessages[^1];
            MessageMock preLastMessage = processedMessages[^2];

            float timeDiff = lastMessage.timestamp - preLastMessage.timestamp;

            return new MessageMock
            {
                timestamp = lastMessage.timestamp + timeDiff, // Delta time to handle network jitter
                position = lastMessage.position + (lastMessage.velocity * timeDiff), // Assuming linear motion (constant velocity) for extrapolation
                velocity = lastMessage.velocity, // Assuming constant velocity
            };
        }

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

            processedMessages.Add(end);

            isLerping = false;
        }

        private Func<MessageMock, MessageMock, float, float, Vector3> GetInterpolation() =>
            interpolationType switch
            {
                InterpolationType.Linear => LinearInterpolation,
                InterpolationType.Hermite => HermiteInterpolation,
                InterpolationType.Bezier => BezierInterpolation,
            };

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
