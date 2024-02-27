using System;
using UnityEngine;

namespace DCL.Multiplayer.Movement.MessageBusMock.Movement
{
    public class Blend : MonoBehaviour
    {
        public float maxSpeed = 30f;
        public float minPositionDelta = 0.1f;

        [Space]
        public float Time;

        private MessageMock startLocal;
        private MessageMock startRemote;

        private Vector3 remoteOldPosition;

        private float totalDuration;
        private float slowedTime;
        private float slowDownFactor;

        public event Action<MessageMock> PointPassed;


        private void Update()
        {
            Time += UnityEngine.Time.deltaTime;

            slowedTime = Time / slowDownFactor;
            if (slowedTime < totalDuration)
            {
                float lerpValue = slowedTime / totalDuration;

                // Interpolate velocity
                var lerpVelocity = startLocal.velocity + ((startRemote.velocity - startLocal.velocity) * lerpValue);

                // Calculate the position at time t
                Vector3 projectedLocal = startLocal.position + (lerpVelocity * slowedTime);
                Vector3 projectedRemote = remoteOldPosition + (startRemote.velocity * slowedTime);

                // Apply the interpolated position
                transform.position = projectedLocal + ((projectedRemote - projectedLocal) * lerpValue);
            }
            else
                enabled = false;
        }

        private void OnEnable()
        {
            Time = 0f;
            slowedTime = 0f;

            totalDuration = startRemote.timestamp - startLocal.timestamp;
            remoteOldPosition = startRemote.position - (startRemote.velocity * totalDuration);

            slowDownFactor = 1f;

            float positionDiff = Vector3.Distance(startLocal.position, startRemote.position);
            float speed = positionDiff / totalDuration;
            if (speed > maxSpeed)
            {
                float desiredDuration = positionDiff / maxSpeed;
                slowDownFactor = desiredDuration / totalDuration;
            }
        }

        private void OnDisable()
        {
            transform.position = startRemote.position;
            PointPassed?.Invoke(startRemote);
        }

        public void Run(MessageMock local, MessageMock remote)
        {
            startLocal = local;
            startRemote = remote;

            if (startLocal.timestamp > startRemote.timestamp) return;

            enabled = true;
        }

        private void CalculateAverageSentRate()
        {
            //     var avarageMessageSentRate = 0f;
            //
            //     if (passedMessages.Count > 4)
            //     {
            //         avarageMessageSentRate += passedMessages[^2].timestamp - passedMessages[^3].timestamp;
            //         avarageMessageSentRate += passedMessages[^3].timestamp - passedMessages[^4].timestamp;
            //         avarageMessageSentRate += passedMessages[^4].timestamp - passedMessages[^5].timestamp;
            //
            //         avarageMessageSentRate /= 3;
            //     }
            //     else if (passedMessages.Count > 3)
            //     {
            //         avarageMessageSentRate += passedMessages[^2].timestamp - passedMessages[^3].timestamp;
            //         avarageMessageSentRate += passedMessages[^3].timestamp - passedMessages[^4].timestamp;
            //
            //         avarageMessageSentRate /= 2;
            //     }
            //     else if (passedMessages.Count > 2) { avarageMessageSentRate += passedMessages[^2].timestamp - passedMessages[^3].timestamp; }
            //
        }
    }
}
