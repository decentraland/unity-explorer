using DCL.Multiplayer.Movement.MessageBusMock;
using UnityEngine;

namespace DCL.Multiplayer.Movement.ECS
{
    public struct BlendComponent
    {
        private const float MAX_SPEED = 30f;
        public float maxExtraTime;

        public bool Enabled;

        private MessageMock startLocal;
        private MessageMock startRemote;

        private float time;
        private Vector3 velocity;

        private Vector3 remoteOldPosition;

        private float blendExtra;
        private float totalDuration;
        private float slowedTime;
        private float slowDownFactor;

        private readonly Transform transform;

        public BlendComponent(Transform transform)
        {
            this.transform = transform;
            Enabled = false;

            time = 0f;
            velocity = Vector3.zero;
            remoteOldPosition = Vector3.zero;
            blendExtra = 0f;
            totalDuration = 0f;
            slowedTime = 0f;
            slowDownFactor = 1f;

            maxExtraTime = 0;
            startLocal = null;
            startRemote = null;
        }

        public (MessageMock startedRemote, MessageMock extra) Update(float deltaTime)
        {
            time += deltaTime;

            slowedTime = time / slowDownFactor;
            if (slowedTime < totalDuration)
            {
                float lerpValue = slowedTime / totalDuration;

                // Interpolate velocity
                velocity = startLocal.velocity + ((startRemote.velocity - startLocal.velocity) * lerpValue);

                // Calculate the position at time t
                Vector3 projectedLocal = startLocal.position + (velocity * slowedTime);
                Vector3 projectedRemote = remoteOldPosition + (startRemote.velocity * slowedTime);

                // Apply the interpolated position
                transform.position = projectedLocal + ((projectedRemote - projectedLocal) * lerpValue);
            }
            else
            {
                var extra = blendExtra > 0f
                    ? new MessageMock
                    {
                        timestamp = startRemote.timestamp + blendExtra,
                        position = transform.position,
                        velocity = startRemote.velocity,
                    }
                    : null;

                Enabled = false;

                return (startRemote, extra);
            }

            return (null, null);
        }

        public void Run(MessageMock local, MessageMock remote)
        {
            startLocal = local;
            startRemote = remote;

            Enable();
        }

        private void Enable()
        {
            // if (positionDiff < minPositionDelta)
            // {
            //     PointPassed?.Invoke(startRemote);
            //     enabled = false;
            //     return;
            // }

            time = 0f;
            slowedTime = 0f;

            float timeDiff = startRemote.timestamp - startLocal.timestamp;
            // blendExtra = Mathf.Clamp(avarageMessageSentRate - timeDiff, 0, maxExtraTime);
            totalDuration = timeDiff + blendExtra;
            remoteOldPosition = startRemote.position - (startRemote.velocity * timeDiff);

            slowDownFactor = 1f;
            float positionDiff = Vector3.Distance(startLocal.position, startRemote.position);
            float speed = positionDiff / totalDuration;
            if (speed > MAX_SPEED)
            {
                float desiredDuration = positionDiff / MAX_SPEED;
                slowDownFactor = desiredDuration / totalDuration;
            }
        }
    }
}
