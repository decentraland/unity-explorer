using DCL.Multiplayer.Movement.MessageBusMock;
using UnityEngine;
using UnityEngine.Serialization;

namespace DCL.Multiplayer.Movement.ECS
{
    public struct ExtrapolationComponent
    {
        private const float MIN_SPEED = 0.01f;

        public readonly Transform Transform;

        public bool Enabled;

        public MessageMock Start;
        public float Time;
        public Vector3 Velocity;

        public ExtrapolationComponent(Transform transform)
        {
            this.Transform = transform;

            Start = null;
            Time = 0f;
            Velocity = Vector3.zero;
            Enabled = false;
        }

        public void Update(float deltaTime)
        {
            Time += deltaTime;

            if (Velocity.sqrMagnitude > MIN_SPEED)
                Transform.position += Velocity * UnityEngine.Time.deltaTime;
        }

        public void Run(MessageMock from)
        {
            Start = from;

            Time = 0f;
            Velocity = from.velocity;
            Enabled = true;
        }
    }
}
