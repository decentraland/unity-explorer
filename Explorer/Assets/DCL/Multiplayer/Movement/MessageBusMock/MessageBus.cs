using System;
using UnityEngine;

namespace DCL.Multiplayer.Movement.MessageBusMock
{
    public class MessageBus : MonoBehaviour
    {
        public Action<MessageMock> MessageSent;

        [Tooltip("Wait for seconds until next sent")]
        public float PackageSentRate;

        [HideInInspector] public float Jitter = 0.1f;
        [HideInInspector] public float InitialLag;

        public void Send(float timestamp, Vector3 position, Vector3 velocity, Vector3 acceleration)
        {
            MessageSent?.Invoke(new MessageMock
            {
                timestamp = timestamp,
                position = position,
                velocity = velocity,
                acceleration = acceleration,
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
