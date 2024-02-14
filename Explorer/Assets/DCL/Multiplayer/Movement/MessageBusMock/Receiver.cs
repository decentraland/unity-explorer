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
        private readonly List<MessageWrap> processedMessages = new ();
        [SerializeField] private MessageBus messageBus;
        [SerializeField] private float minDelta;
        [SerializeField] private InterpolationType interpolationType;

        [SerializeField] private bool useAcceleration;
        [SerializeField] private bool useVelocity;

        private bool isLerping;

        private Func<MessageMock, MessageMock, float, float, Vector3> interpolation;

        [Space]
        [Header("DEBUG")]
        public int Incoming;
        public int Processed;

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
                // Process incoming message
                MessageMock newMessage = incomingMessages.Dequeue();
                HandleNewMessage(newMessage);
            }
            else if (processedMessages.Count >= 2)
            {
                ExtrapolateNextPosition();
            }

            if (processedMessages.Count < 2) return; // Ensure we have at least two messages to interpolate between

            MessageMock startPoint = processedMessages[^2];
            MessageMock endPoint = processedMessages[^1];

            interpolation = interpolationType switch
                            {
                                InterpolationType.Linear => LinearInterpolation,
                                InterpolationType.Hermite => HermiteInterpolation,
                                InterpolationType.Bezier => BezierInterpolation,
                            };

            if (Vector3.Distance(startPoint.position, endPoint.position) > minDelta)
                StartCoroutine(MoveTo(startPoint, endPoint, interpolation));
        }

        private void ExtrapolateNextPosition()
        {
            // Get the last two real messages to calculate the extrapolation
            var lastMessage = processedMessages[^1];
            var preLastMessage = processedMessages[^2];
            var timeDiff = lastMessage.timestamp - preLastMessage.timestamp;

            // Assuming linear motion (constant velocity) for extrapolation
            var extrapolatedPosition = lastMessage.position + (lastMessage.velocity * timeDiff);
            var extrapolatedTimestamp = lastMessage.timestamp + timeDiff;

            // Create an artificial message with extrapolated values
            MessageMock extrapolatedMessage = new MessageMock
            {
                position = extrapolatedPosition,
                timestamp = extrapolatedTimestamp,
                velocity = lastMessage.velocity, // Assuming constant velocity
            };

            processedMessages.Add(new MessageWrap(extrapolatedMessage));

            if (processedMessages.Count > 100)
                processedMessages.RemoveAt(0);
        }

        private void HandleNewMessage(MessageMock newMessage)
        {
            if (processedMessages.Count == 0)
            {
                transform.position = newMessage.position;
            }
            else if (!useVelocity)
            {
                MessageMock lastMessage = processedMessages[^1];

                lastMessage.velocity = CalculateDiff(lastMessage.position, lastMessage.timestamp, newMessage.position, newMessage.timestamp);

                newMessage.velocity = useAcceleration
                    ? lastMessage.velocity + (lastMessage.acceleration * (newMessage.timestamp - lastMessage.timestamp))
                    : lastMessage.velocity;
            }

            processedMessages.Add(new MessageWrap(newMessage));

            // Remove oldest to keep list size in check
            if (processedMessages.Count > 100)
                processedMessages.RemoveAt(0);
        }

        private static Vector3 CalculateDiff(Vector3 start, float startT, Vector3 end, float endT) =>
            (end - start) / (endT - startT);

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
    }

    public class MessageWrap : MessageMock
    {
        public bool isPassed;

        public MessageWrap(MessageMock message, bool isPassed = false)
        {
            timestamp = message.timestamp;
            position = message.position;
            velocity = message.velocity;
            acceleration = message.acceleration;
            this.isPassed = isPassed;
        }
    }
}
