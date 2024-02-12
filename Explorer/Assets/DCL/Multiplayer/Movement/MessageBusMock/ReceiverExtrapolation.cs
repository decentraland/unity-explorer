using System;
using System.Collections.Generic;
using UnityEngine;
using Debug = System.Diagnostics.Debug;

namespace DCL.Multiplayer.Movement.MessageBusMock
{
    public class ReceiverExtrapolation : MonoBehaviour
    {
        public const int MAX_POSITIONS = 10;

        private readonly Queue<MessageMock> receivedMessages = new ();
        private readonly List<MessageMock> receivedHistory = new ();
        private readonly List<MessageMock> replicaHistory = new ();

        [SerializeField] private MessageBus messageBus;

        private Vector3 currentVelocity = Vector3.zero;
        private Vector3? target;

        private void Awake()
        {
            messageBus.MessageSent += OnMessageReceived;
        }

        private void Update()
        {
            if (target != null)
            {
                transform.position = Vector3.MoveTowards(transform.position, target.Value, currentVelocity.magnitude * UnityEngine.Time.deltaTime);

                if (Vector3.Distance(transform.position, target.Value) < 0.001f)
                    target = null;
            }
            else
                transform.position += currentVelocity * UnityEngine.Time.deltaTime;
        }

        private void OnMessageReceived(MessageMock newMessage)
        {
            if (replicaHistory.Count == 0)
            {
                transform.position = newMessage.position;
                currentVelocity = newMessage.velocity;
            }
            else
            {
                target = newMessage.position;
                currentVelocity = newMessage.velocity;
            }
        }
    }

    public static class ListExtensions
    {
        public static void AddToHistory(this List<MessageMock> history, MessageMock newPosition)
        {
            while (history.Count >= ReceiverExtrapolation.MAX_POSITIONS)
                history.RemoveAt(0);

            history.Add(newPosition);
        }
    }
}
