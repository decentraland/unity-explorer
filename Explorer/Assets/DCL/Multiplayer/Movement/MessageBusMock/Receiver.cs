using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace DCL.Multiplayer.Movement.MessageBusMock
{
    public enum InterpolationType
    {
        Linear,
        Hermite,
    }

    public class Receiver : MonoBehaviour
    {
        private readonly Queue<MessageMock> receivedMessages = new ();
        [SerializeField] private MessageBus messageBus;
        [SerializeField] private float minDelta;
        [SerializeField] private InterpolationType interpolationType;

        private bool isLerping;
        private Coroutine coroutine;

        private Func<MessageMock, MessageMock, float, float, Vector3> interpolation;

        private void Awake()
        {
            messageBus.MessageSent += OnMessageReceived;

            interpolation = interpolationType switch
                            {
                                InterpolationType.Linear => LinearInterpolation,
                                InterpolationType.Hermite => HermiteInterpolation,
                            };
        }

        private void OnMessageReceived(MessageMock newMessage)
        {
            if (history.Count < 2)
            {
                transform.position = newMessage.position;
                AddToHistory(newMessage);
                return;
            }

            // else
            //     receivedMessages.Enqueue(newMessage);

            if (isLerping || history.Count < 2) return;

            var acceleration1 = (newMessage.velocity - history[^1].velocity) / (newMessage.timestamp - history[^1].timestamp);
            var acceleration2 = (history[^1].velocity - history[^2].velocity) / (history[^1].timestamp - history[^2].timestamp);
            var jerk = (acceleration1 - acceleration2) / (history[^1].timestamp - history[^2].timestamp);
            var acceleration = Vector3.zero;// acceleration1 + (jerk * messageBus.PackageSentRate);

            transform.position = newMessage.position;

            var endPoint = new MessageMock
            {
                timestamp = newMessage.timestamp + messageBus.PackageSentRate,
                position = newMessage.position + (newMessage.velocity * messageBus.PackageSentRate),
                velocity = newMessage.velocity + (acceleration * messageBus.PackageSentRate),
            };

            StartCoroutine(MoveTo(newMessage, endPoint, interpolation));
        }

        // private void Update()
        // {
        //     if (isLerping) return;
        //
        //     MessageMock startPoint = null;
        //     MessageMock endPoint = null;
        //
        //     if (receivedMessages.Count > 0 && history.Count > 0)
        //     {
        //         startPoint = history.Last();
        //
        //         endPoint = new MessageMock
        //         {
        //             timestamp = UnityEngine.Time.unscaledTime + messageBus.PackageSentRate,
        //             position = startPoint.position + (startPoint.velocity * messageBus.PackageSentRate),
        //             velocity = startPoint.velocity,
        //         };
        //     }
        //
        //     // (startPoint, endPoint) = Interpolation(startPoint, endPoint);
        //
        //     if (startPoint == null || endPoint == null)
        //         return;
        //
        //     if (Vector3.Distance(startPoint.position, endPoint.position) > minDelta)
        //         StartCoroutine(MoveTo(startPoint, endPoint, interpolation));
        // }

        private (MessageMock, MessageMock) Interpolation(MessageMock startPoint, MessageMock endPoint)
        {
            if (receivedMessages.Count > 1)
            {
                startPoint = receivedMessages.Dequeue();
                endPoint = receivedMessages.Peek();

                AddToHistory(startPoint);
                Debug.Log("NEW");
            }
            else if (receivedMessages.Count == 1 && history.Count > 0)
            {
                startPoint = history.Last();
                endPoint = receivedMessages.Peek();

                Debug.Log("INTERPOLATION");
            }
            else if (receivedMessages.Count == 0 && history.Count > 0)
            {
                // Velocity not changed
                startPoint = history.Last();

                // Caclulate end point
                endPoint = new MessageMock
                {
                    timestamp = UnityEngine.Time.unscaledTime + messageBus.PackageSentRate,
                    position = startPoint.position + (startPoint.velocity * messageBus.PackageSentRate),
                    velocity = startPoint.velocity,
                };

                history.Add(endPoint);

                Debug.Log("EXTRAPOLATION");
            }

            return (startPoint, endPoint);
        }

        private readonly List<MessageMock> history = new ();
        private const int MAX_POSITIONS = 10;

        private void AddToHistory(MessageMock newPosition)
        {
            while (history.Count >= MAX_POSITIONS)
                history.RemoveAt(0);

            history.Add(newPosition);
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

            AddToHistory(end);
            isLerping = false;
        }

        private static Vector3 LinearInterpolation(MessageMock start, MessageMock end, float t, float _) =>
            Vector3.Lerp(start.position, end.position, t);

        /// Cubic Hermite Spline version of Hermite interpolation
        private static Vector3 HermiteInterpolation(MessageMock start, MessageMock end, float t, float timeDif)
        {
            float t2 = t * t;
            float t3 = t2 * t;

            float h2 = (-2 * t3) + (3 * t2); // Hermite basis function h_01 (for end position)
            float h1 = -h2 + 1; // Hermite basis function h_00 (for start position)

            float h3 = t3 - (2 * t2) + t; // Hermite basis function h_10 (for start velocity)
            float h4 = t3 - t2; // Hermite basis function h_11 (for end velocity)

            return (h1 * start.position) + (h2 * end.position) + (start.velocity * (h3 * timeDif)) + (end.velocity * (h4 * timeDif));
        }
    }
}
