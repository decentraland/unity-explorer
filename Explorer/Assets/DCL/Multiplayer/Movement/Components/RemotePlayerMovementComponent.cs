#nullable enable

using DCL.CharacterMotion.Animation;
using DCL.CharacterMotion.Settings;
using System;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Pool;
using Utility.PriorityQueue;

namespace DCL.Multiplayer.Movement
{
    public struct RemotePlayerMovementComponent : IDisposable
    {
        public const string TEST_ID = "SelfReplica";
        private const short MAX_MESSAGES = 10;

        private readonly BoundedNetworkMessageQueue queue;
        private bool disposed;

        public NetworkMovementMessage PastMessage;

        public bool Initialized;
        public bool WasTeleported;
        public bool WasPassedThisFrame;

        public readonly BoundedNetworkMessageQueue? Queue => disposed ? null : queue;

        public float InitialCooldownTime;

        public bool HeadIKYawEnabled;
        public bool HeadIKPitchEnabled;
        public float2 HeadIKYawAndPitch;
        public bool IsPointingAt;
        public float3 PointAtWorldHitPoint;
        public float PointAtTimeToLive;

        public RemotePlayerMovementComponent(IObjectPool<SimplePriorityQueue<NetworkMovementMessage, double>> queuePool)
        {
            queue = new BoundedNetworkMessageQueue(MAX_MESSAGES);
            disposed = false;

            PastMessage = new NetworkMovementMessage();
            Initialized = false;
            WasTeleported = false;

            WasPassedThisFrame = false;

            InitialCooldownTime = 0;

            HeadIKYawEnabled = false;
            HeadIKPitchEnabled = false;
            HeadIKYawAndPitch = float2.zero;

            IsPointingAt = false;
            PointAtWorldHitPoint = float3.zero;
            PointAtTimeToLive = 0f;
        }

        public void Enqueue(NetworkMovementMessage message)
        {
            queue.Enqueue(message);
        }

        public void AddPassed(NetworkMovementMessage message, ICharacterControllerSettings settings, bool wasTeleported = false)
        {
            if (!WasTeleported)
            {
                var totalDuration = (float)(message.timestamp - PastMessage.timestamp);

                message.animState.MovementBlendValue = AnimationMovementBlendLogic.CalculateBlendValue(totalDuration, PastMessage.animState.MovementBlendValue,
                    message.movementKind, message.velocitySqrMagnitude, settings);

                message.animState.SlideBlendValue = AnimationSlideBlendLogic.CalculateBlendValue(totalDuration, PastMessage.animState.SlideBlendValue, message.isSliding, settings);
            }

            PastMessage = message;
            WasTeleported = wasTeleported;

            WasPassedThisFrame = true;
        }

        public void UpdateHeadIK(in NetworkMovementMessage message)
        {
            HeadIKYawEnabled = message.headIKYawEnabled;
            HeadIKPitchEnabled = message.headIKPitchEnabled;
            HeadIKYawAndPitch = message.headYawAndPitch;
        }

        public void UpdatePointAtIK(in NetworkMovementMessage message, float pointAtDuration)
        {
            IsPointingAt = message.isPointingAt;
            PointAtWorldHitPoint = message.pointAtWorldHitPoint;
            PointAtTimeToLive = message.isPointingAt ? pointAtDuration : 0f;
        }

        public void TickPointAtExpiry(float deltaTime)
        {
            if (!IsPointingAt)
                return;

            PointAtTimeToLive -= deltaTime;

            if (PointAtTimeToLive <= 0f)
            {
                PointAtTimeToLive = 0f;
                IsPointingAt = false;
            }
        }

        public void Dispose()
        {
            disposed = true;
            queue.Clear();
        }
    }
}
