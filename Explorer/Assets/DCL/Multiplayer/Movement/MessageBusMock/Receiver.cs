using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace DCL.Multiplayer.Movement.MessageBusMock
{
    public class Receiver : MonoBehaviour
    {
        private readonly Queue<MessageMock> receivedMessages = new ();
        [SerializeField] private MessageBus messageBus;
        [SerializeField] private float minDelta;

        private bool isLerping;
        private Coroutine coroutine;

        private bool isFirst;

        private void Awake() =>
            messageBus.MessageSent += newMessage =>
            {
                if (isFirst)
                {
                    transform.position = newMessage.position;
                    isFirst = false;
                }

                receivedMessages.Enqueue(newMessage);
            };

        private void Update()
        {
            if (receivedMessages.Count > 1 && !isLerping)
            {
                MessageMock startPoint = receivedMessages.Dequeue();
                MessageMock endPoint = receivedMessages.Peek();

                if (Vector3.Distance(startPoint.position, endPoint.position) > minDelta)
                    StartCoroutine(MoveToHermite(startPoint, endPoint));
            }
        }

        private IEnumerator MoveToLinearly(MessageMock start, MessageMock end)
        {
            isLerping = true;

            var t = 0f;
            float timeDif = end.timestamp - start.timestamp;

            while (t < timeDif)
            {
                t += UnityEngine.Time.deltaTime;
                transform.position = Vector3.Lerp(start.position, end.position, t / timeDif);
                yield return null;
            }

            isLerping = false;
        }

        private IEnumerator MoveToHermite(MessageMock start, MessageMock end)
        {
            isLerping = true;

            var t = 0.0f;
            float timeDif = end.timestamp - start.timestamp;

            while (t < timeDif)
            {
                t += UnityEngine.Time.deltaTime;
                transform.position = CubicHermiteSpline(start, end, t / timeDif, timeDif);
                yield return null;
            }

            isLerping = false;
        }

        private static Vector3 CubicHermiteSpline(MessageMock start, MessageMock end, float t, float timeDif)
        {
            float t2 = t * t;
            float t3 = t2 * t;

            float h2 = (-2 * t3) + (3 * t2); // end position h_01
            float h1 = -h2 + 1; // start position h_00

            float h3 = t3 - (2 * t2) + t; // start velocity h_10
            float h4 = t3 - t2; // end velocity h_11

            return (h1 * start.position) + (h2 * end.position) + (start.velocity * (h3 * timeDif)) + (end.velocity * (h4 * timeDif));
        }
    }
}
