using System;
using UnityEngine;

namespace DCL.Multiplayer.Movement.MessageBusMock
{
    public class MessageBus : MonoBehaviour
    {
        public Action<MessageMock> MessageSent;

        [Tooltip("Wait for seconds until next sent")]
        public float PackageSentRate;
        public float Jitter = 0.1f;

        public float InitialLag;

        public void Send(float timestamp, Vector3 position, Vector3 velocity)
        {
            MessageSent?.Invoke(new MessageMock
            {
                timestamp = timestamp,
                position = position,
                velocity = velocity,
            });
        }
    }

    [Serializable]
    public class MessageMock
    {
        public float timestamp;
        public Vector3 position;
        public Vector3 velocity;
        public Vector3 acceleration;
    }
}
