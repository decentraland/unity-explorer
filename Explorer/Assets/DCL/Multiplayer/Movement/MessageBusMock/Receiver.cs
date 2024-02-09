using System;
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

        [Header("DEBUG")]
        [SerializeField] private float lerpTime;

        private bool isLerping;
        private Coroutine coroutine;
        private Vector3 targetPosition;

        private void Awake() =>
            messageBus.MessageSent += newMessage => receivedMessages.Enqueue(newMessage);

        private void Update()
        {
            if (receivedMessages.Count > 1 && !isLerping)
            {
                MessageMock nextTarget = receivedMessages.Dequeue();

                if (Vector3.Distance(transform.position, nextTarget.position) > minDelta)
                {
                    lerpTime = receivedMessages.Peek().timestamp - nextTarget.timestamp;
                    StartCoroutine(MoveToLinearly(nextTarget.position, lerpTime));
                }
            }
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
    }
}
