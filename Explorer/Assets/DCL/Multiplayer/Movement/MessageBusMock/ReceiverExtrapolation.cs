using System;
using UnityEngine;

namespace DCL.Multiplayer.Movement.MessageBusMock
{
    public class ReceiverExtrapolation : MonoBehaviour
    {
        public MessageBus messageBus;

        [Space]
        public InterpolationType extrapolationType;
        public bool useAcceleration;

        private MessageMock local;
        private MessageMock remote;

        private float t; // Time since the new package was received

        private bool firstMessage = true;

        private MessageMock projectedRemote;

        private void Awake()
        {
            messageBus.MessageSent += OnMessageReceived;
        }

        private void Update()
        {
            if (local == null) return;

            t += Time.deltaTime;

            if (t > messageBus.PackageSentRate)
                transform.position += local.velocity * Time.deltaTime;
            else
            {
                transform.position = extrapolationType switch
                                     {
                                         InterpolationType.VelocityBlending => ProjectiveVelocityBlending(local, remote, t, messageBus.PackageSentRate),
                                         InterpolationType.Hermite => Interpolate.Hermite(local, projectedRemote, t, messageBus.PackageSentRate),
                                         InterpolationType.Bezier => Interpolate.Bezier(local, projectedRemote, t, messageBus.PackageSentRate),
                                         InterpolationType.Linear => Interpolate.Linear(local, projectedRemote, t, messageBus.PackageSentRate),
                                         InterpolationType.MonotoneYHermite => Interpolate.MonotoneYHermite(local, projectedRemote, t, messageBus.PackageSentRate),
                                         InterpolationType.FullMonotonicHermite => Interpolate.FullMonotonicHermite(local, projectedRemote, t, messageBus.PackageSentRate),
                                         InterpolationType.PositionBlending => Interpolate.ProjectivePositionBlending(local, projectedRemote, t, messageBus.PackageSentRate),
                                         _ => throw new ArgumentOutOfRangeException()
                                     };
            }
        }

        private static Vector3 ProjectiveVelocityBlending(MessageMock local, MessageMock remote, float t, float totalDuration)
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
            if (firstMessage)
            {
                transform.position = newMessage.position;
                local = newMessage;
                t = messageBus.PackageSentRate;
                firstMessage = false;
            }
            else
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

            remote = new MessageMock
            {
                position = newMessage.position,
                velocity = newMessage.velocity,
                acceleration = useAcceleration ? newMessage.acceleration : Vector3.zero,
            };

            projectedRemote = new MessageMock
            {
                timestamp = remote.timestamp + messageBus.PackageSentRate,
                position = remote.position + (remote.velocity * messageBus.PackageSentRate) + (remote.acceleration * (0.5f * messageBus.PackageSentRate * messageBus.PackageSentRate)),
                velocity = remote.velocity + (remote.acceleration * messageBus.PackageSentRate),
                acceleration = remote.acceleration,
            };
        }
    }
}
