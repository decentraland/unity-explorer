using System;
using UnityEngine;
using UnityEngine.Serialization;
using static DCL.CharacterMotion.Components.CharacterAnimationComponent;

namespace DCL.Multiplayer.Movement.MessageBusMock
{
    public class MessageBus : MonoBehaviour
    {
        public Action<MessageMock> MessageSent;

        [Tooltip("Wait for seconds until next sent")]
        public float PackageSentRate;
        public float PackagesJitter = 0.1f;

        [Space] public float Latency;
        public float LatencyJitter;

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

        public AnimationStates animState;
        public bool isStunned;
    }
}
