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
                    StartCoroutine(MoveToLinearly2(startPoint.position, endPoint.position, endPoint.timestamp - startPoint.timestamp));
                    // StartCoroutine(MoveToLinearly(next.position, receivedMessages.Peek().timestamp - next.timestamp));
            }
        }

        private IEnumerator MoveToLinearly2(Vector3 initialPosition, Vector3 targetPosition, float timeDif)
        {
            isLerping = true;

            var t = 0f;

            while (t < timeDif)
            {
                t += UnityEngine.Time.deltaTime;
                transform.position = Vector3.Lerp(initialPosition, targetPosition, t / timeDif);
                yield return null;
            }

            isLerping = false;
        }

        private IEnumerator MoveToLinearly(Vector3 targetPosition, float timeDif)
        {
            isLerping = true;

            Vector3 initialPosition = transform.position;

            var t = 0f;

            while (t < timeDif)
            {
                t += UnityEngine.Time.deltaTime;
                transform.position = Vector3.Lerp(initialPosition, targetPosition, t / timeDif);
                yield return null;
            }

            isLerping = false;
        }

        private IEnumerator MoveToHermite(MessageMock start, MessageMock end)
        {
            var timeDif = end.timestamp - start.timestamp;

            isLerping = true;
            var t = 0.0f;

            while (t < timeDif)
            {
                t += UnityEngine.Time.deltaTime;

                float s = t / timeDif; // Normalized time [0, 1]
                float h1 = (2 * s * s * s) - (3 * s * s) + 1;
                float h2 = (-2 * s * s * s) + (3 * s * s);
                float h3 = (s * s * s) - (2 * s * s) + s;
                float h4 = (s * s * s) - (s * s);

                Vector3 position = (h1 * start.position) + (h2 * end.position) + (h3 * start.velocity * timeDif) + (h4 * end.velocity * timeDif);

                transform.position = position;

                yield return null;
            }

            isLerping = false;
        }

    }
}
