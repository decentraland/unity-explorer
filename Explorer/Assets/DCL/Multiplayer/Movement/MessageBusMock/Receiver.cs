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
    }
    public class Receiver : MonoBehaviour
    {
        private readonly Queue<MessageMock> receivedMessages = new ();
        [SerializeField] private MessageBus messageBus;
        [SerializeField] private float minDelta;
        [SerializeField] private InterpolationType interpolationType;

        private bool isLerping;
        private Coroutine coroutine;

        private bool isFirst;
        private Func<MessageMock, MessageMock, float, float, Vector3> interpolation;

        private void Awake()
        {
            messageBus.MessageSent += newMessage =>
            {
                if (isFirst)
                {
                    transform.position = newMessage.position;
                    isFirst = false;
                }

                receivedMessages.Enqueue(newMessage);
            };

            interpolation = interpolationType switch
                            {
                                InterpolationType.Linear => LinearInterpolation,
                                InterpolationType.Hermite => HermiteInterpolation,
                            };
        }

        private void Update()
        {
            if (receivedMessages.Count > 1 && !isLerping)
            {
                MessageMock startPoint = receivedMessages.Dequeue();
                MessageMock endPoint = receivedMessages.Peek();

                if (Vector3.Distance(startPoint.position, endPoint.position) > minDelta)
                    StartCoroutine(MoveTo(startPoint, endPoint, interpolation));
            }
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
