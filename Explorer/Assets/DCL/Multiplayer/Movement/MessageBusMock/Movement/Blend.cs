using System;
using UnityEngine;

namespace DCL.Multiplayer.Movement.MessageBusMock.Movement
{
    public class Blend : MonoBehaviour
    {
        public float maxSpeed = 30f;
        public float maxExtraTime;

        [Space]
        public float Time;
        public Vector3 Velocity;

        public float blendExtra;

        private float totalDuration;

        private MessageMock startLocal;
        private MessageMock startRemote;

        private Vector3 remoteOldPosition;

        public event Action<MessageMock> PointPassed;

        private void Update()
        {
            // blendDuration += Time.deltaTime;
            // Time += UnityEngine.Time.deltaTime/ slowDownFactor;

            Time += UnityEngine.Time.deltaTime;

            if (Time < totalDuration)
            {
                float lerpValue = Time / totalDuration;

                // Interpolate velocity
                Velocity = startLocal.velocity + ((startRemote.velocity - startLocal.velocity) * lerpValue);

                // Calculate the position at time t
                Vector3 projectedLocal = startLocal.position + (Velocity * Time);
                Vector3 projectedRemote = remoteOldPosition + (startRemote.velocity * Time);

                // Apply the interpolated position
                transform.position = projectedLocal + ((projectedRemote - projectedLocal) * lerpValue);
            }
            else
            {
                PointPassed?.Invoke(startRemote);

                if (blendExtra > 0f)
                    PointPassed?.Invoke(new MessageMock
                    {
                        timestamp = startRemote.timestamp + blendExtra,
                        position = transform.position,
                        velocity = startRemote.velocity,
                    });

                enabled = false;
            }
        }

        private void OnEnable()
        {
            {
            //     float positionDiff = Vector3.Distance(local.position, remote.position);
            //
            //     if (positionDiff < minPositionDelta)
            //     {
            //         AddToPassed(remote);
            //         isBlending = false;
            //         yield break;
            //     }
            //
            //     float timeDiff = remote.timestamp - local.timestamp;
            //     blendExtra = Mathf.Clamp(avarageMessageSentRate - timeDiff, 0, maxBlendExtraTime);
            //
            //     // Debug.Log($"{blendExtra} | {timeDiff} | {avarageMessageSentRate}  |  {avarageMessageSentRate - timeDiff}");
            //
            //     float totalDuration = timeDiff + blendExtra;
            //
            //     var slowDownFactor = 1f;
            //     float speed = positionDiff / totalDuration;
            //
            //     if (speed > maxBlendSpeed)
            //     {
            //         float desiredDuration = positionDiff / maxBlendSpeed;
            //         slowDownFactor = desiredDuration / totalDuration;
            //     }
            //
            //     var t = 0f;
            //     blendDuration = 0f;
            }

            Time = 0f;

            float timeDiff = startRemote.timestamp - startLocal.timestamp;
            totalDuration = timeDiff + blendExtra;

            remoteOldPosition = startRemote.position - (startRemote.velocity * timeDiff);
        }

        public void Run(MessageMock local, MessageMock remote)
        {
            startLocal = local;
            startRemote = remote;

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
