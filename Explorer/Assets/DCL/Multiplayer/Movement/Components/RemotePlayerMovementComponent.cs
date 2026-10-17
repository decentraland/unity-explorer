using DCL.CharacterMotion.Animation;
using DCL.CharacterMotion.Settings;
using System;
using Unity.Mathematics;
using UnityEngine.Pool;
using Utility.PriorityQueue;

namespace DCL.Multiplayer.Movement
{
    public struct RemotePlayerMovementComponent : IDisposable
    {
        public const string TEST_ID = "SelfReplica";
        private const short MAX_MESSAGES = 10;
        private const double RESET_TIMELINE_PRIORITY = double.MinValue;

        private readonly IObjectPool<SimplePriorityQueue<NetworkMovementMessage, double>> queuePool;
        private readonly SimplePriorityQueue<NetworkMovementMessage, double> queue;
        private bool disposed;

        public NetworkMovementMessage PastMessage;

        public bool Initialized;
        public bool WasTeleported;
        public bool WasPassedThisFrame;

        public readonly SimplePriorityQueue<NetworkMovementMessage, double>? Queue => disposed ? null : queue;

        public float InitialCooldownTime;

        public bool HeadIkYawEnabled;
        public bool HeadIkPitchEnabled;
        public float2 HeadIkYawAndPitch;
        public bool IsPointingAt;
        public float3 PointAtWorldHitPoint;

        public RemotePlayerMovementComponent(IObjectPool<SimplePriorityQueue<NetworkMovementMessage, double>> queuePool)
        {
            this.queuePool = queuePool;
            queue = queuePool.Get()!;
            disposed = false;

            PastMessage = new NetworkMovementMessage();
            Initialized = false;
            WasTeleported = false;

            WasPassedThisFrame = false;

            InitialCooldownTime = 0;

            HeadIkYawEnabled = false;
            HeadIkPitchEnabled = false;
            HeadIkYawAndPitch = float2.zero;

            IsPointingAt = false;
            PointAtWorldHitPoint = float3.zero;
        }

        public void Enqueue(NetworkMovementMessage message)
        {
            if (message.resetTimeline)
                queue.Clear();

            while (queue.Count > MAX_MESSAGES)
            {
                if (queue.First.resetTimeline)
                {
                    NetworkMovementMessage resetMessage = queue.Dequeue();
                    queue.Dequeue();
                    queue.Enqueue(resetMessage, RESET_TIMELINE_PRIORITY);
                }
                else
                    queue.Dequeue();
            }

            queue.Enqueue(message, message.resetTimeline ? RESET_TIMELINE_PRIORITY : message.timestamp);
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

        public void UpdateHeadIk(in NetworkMovementMessage message)
        {
            HeadIkYawEnabled = message.headIKYawEnabled;
            HeadIkPitchEnabled = message.headIKPitchEnabled;
            HeadIkYawAndPitch = message.headYawAndPitch;
        }

        public void UpdatePointAtIk(in NetworkMovementMessage message)
        {
            IsPointingAt = message.isPointingAt;
            PointAtWorldHitPoint = message.pointAtWorldHitPoint;
        }

        public void Dispose()
        {
            disposed = true;
            queuePool.Release(queue);
        }
    }
}
