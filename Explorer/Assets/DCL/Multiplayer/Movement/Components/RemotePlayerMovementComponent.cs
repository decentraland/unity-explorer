#nullable enable

using DCL.CharacterMotion.Animation;
using DCL.CharacterMotion.Settings;
using System;
using UnityEngine.Pool;
using Utility.PriorityQueue;

namespace DCL.Multiplayer.Movement
{
    public struct RemotePlayerMovementComponent : IDisposable
    {
        public const string TEST_ID = "SelfReplica";
        private const short MAX_MESSAGES = 10;

        private readonly IObjectPool<SimplePriorityQueue<NetworkMovementMessage>> queuePool;
        private readonly SimplePriorityQueue<NetworkMovementMessage> queue;
        private bool disposed;

        public NetworkMovementMessage PastMessage;

        public bool Initialized;
        public bool WasTeleported;
        public bool WasPassedThisFrame;

        public readonly SimplePriorityQueue<NetworkMovementMessage>? Queue => disposed ? null : queue;

        public RemotePlayerMovementComponent(IObjectPool<SimplePriorityQueue<NetworkMovementMessage>> queuePool)
        {
            this.queuePool = queuePool;
            queue = queuePool.Get()!;
            disposed = false;

            PastMessage = new NetworkMovementMessage();
            Initialized = false;
            WasTeleported = false;

            WasPassedThisFrame = false;
        }

        public void Enqueue(NetworkMovementMessage message)
        {
            while (queue.Count > MAX_MESSAGES)
                queue.Dequeue();

            queue.Enqueue(message, message.timestamp);
        }

        public void AddPassed(NetworkMovementMessage message, ICharacterControllerSettings settings, bool wasTeleported = false)
        {
            if (!WasTeleported)
            {
                float totalDuration = message.timestamp - PastMessage.timestamp;

                int movementBlendId = AnimationMovementBlendLogic.GetMovementBlendId(message.velocity.sqrMagnitude, message.movementKind);

                message.animState.MovementBlendValue = AnimationMovementBlendLogic.CalculateBlendValue(totalDuration, PastMessage.animState.MovementBlendValue,
                    movementBlendId, message.movementKind, message.velocity.magnitude, settings);

                message.animState.SlideBlendValue = AnimationSlideBlendLogic.CalculateBlendValue(totalDuration, PastMessage.animState.SlideBlendValue, message.isSliding, settings);
            }

            PastMessage = message;
            WasTeleported = wasTeleported;

            WasPassedThisFrame = true;
        }

        public void Dispose()
        {
            disposed = true;
            queuePool.Release(queue);
        }
    }
}
