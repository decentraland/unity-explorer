using UnityEngine;

namespace DCL.Multiplayer.Movement.MessageBusMock
{
    public class ReceiverExtrapolation : MonoBehaviour
    {
        [SerializeField] private MessageBus messageBus;
        [SerializeField] private bool useAcceleration;

        private MessageMock local;
        private MessageMock remote;

        private float t; // Time since the new package was received

        private bool firstMessage = true;

        private void Awake()
        {
            messageBus.MessageSent += OnMessageReceived;
        }

        private void Update()
        {
            if (remote == null) return;

            t += UnityEngine.Time.deltaTime;
            transform.position = VelocityBlendingInterpolation(local, remote, t, messageBus.PackageSentRate);
        }

        private static Vector3 VelocityBlendingInterpolation(MessageMock local, MessageMock remote, float t, float totalDuration)
        {
            float lerpValue = Mathf.Max(0, t / totalDuration);

            Vector3 projectedRemote = remote.position + (remote.velocity * t) + (remote.acceleration * (0.5f * t * t));

            if (lerpValue < 1f)
            {
                Vector3 lerpedVelocity = local.velocity + ((remote.velocity - local.velocity) * lerpValue); // Interpolated velocity
                Vector3 projectedLocal = local.position + (lerpedVelocity * t) + (remote.acceleration * (0.5f * t * t));

                return projectedLocal + ((projectedRemote - projectedLocal) * lerpValue); // interpolate positions
            }

            return projectedRemote;
        }

        private void OnMessageReceived(MessageMock newMessage)
        {
            remote = new MessageMock
            {
                position = newMessage.position,
                velocity = newMessage.velocity,
                acceleration = useAcceleration ? newMessage.acceleration : Vector3.zero,
            };

            if (!firstMessage)
            {
                // Current local state at the time of the new package
                local = new MessageMock
                {
                    position = transform.position,
                    velocity = remote.velocity,
                    acceleration = remote.acceleration,
                };

                t = 0f; // Reset time since the new package
            }
            else
            {
                transform.position = remote.position;
                local = remote;
                t = messageBus.PackageSentRate;
                firstMessage = false;
            }
        }
    }
}
