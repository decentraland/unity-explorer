using System;
using UnityEngine;

namespace DCL.Multiplayer.Movement.MessageBusMock
{
    public class MessageBus : MonoBehaviour
    {
        public Action<MessageMock> MessageSent;

        [Tooltip("Wait for seconds until next sent")]
        public float PackageSentRate;
        public float InitialLag;

        public void Send(float timestamp, Vector3 position)
        {
            MessageSent?.Invoke(new MessageMock()
            {
                timestamp = timestamp,
                position = position,
            });
        }
    }

    [Serializable]
    public class MessageMock
    {
        public float timestamp;
        public Vector3 position;
    }
}
