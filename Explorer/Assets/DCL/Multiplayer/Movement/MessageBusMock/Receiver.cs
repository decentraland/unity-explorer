using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace DCL.Multiplayer.Movement.MessageBusMock
{
    public class Receiver : MonoBehaviour
    {
        [SerializeField] private int listCapacity;

        [SerializeField] private MessageBus messageBus;
        [SerializeField] private float positionPrecision;

        [Space]
        [SerializeField] private List<MessageMock> receivedMessages;
        [SerializeField] private float lerpTime;

        [SerializeField] private MessageMock lastMessage;

        private Coroutine coroutine;

        private void Start()
        {
            receivedMessages = new List<MessageMock>(listCapacity);
            messageBus.MessageSent += OnReceive;
        }

        private void OnReceive(MessageMock newMessage)
        {
            float lastTimestamp = lastMessage?.timestamp ?? 0;
            lastMessage = newMessage;

            lerpTime = newMessage.timestamp - lastTimestamp;

            if (coroutine != null) StopCoroutine(coroutine);
            coroutine = StartCoroutine(LerpToNewPosition(newMessage.position, lerpTime));
        }

        private IEnumerator LerpToNewPosition(Vector3 targetPosition, float timeDif)
        {
            Vector3 initialPosition = transform.position;

            var t = 0f;
            while (t < timeDif)
            {
                t += UnityEngine.Time.deltaTime;
                transform.position = Vector3.Lerp(initialPosition, targetPosition, t / timeDif);
                yield return null;
            }
        }
    }
}
