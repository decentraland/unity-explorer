#nullable enable

using System;
using UnityEngine.Pool;
using Utility.PriorityQueue;

namespace DCL.Multiplayer.Movement
{
    public struct RemotePlayerMovementComponent : IDisposable
    {
        public const string TEST_ID = "SelfReplica";
        private const short MAX_MESSAGES = 10;

        private readonly IObjectPool<IPriorityQueue<NetworkMovementMessage, float>> queuePool;
        private readonly IPriorityQueue<NetworkMovementMessage, float> queue;
        private bool disposed;

        public NetworkMovementMessage PastMessage;

        public bool Initialized;
        public bool WasTeleported;
        public bool WasPassedThisFrame;

        public readonly IPriorityQueue<NetworkMovementMessage, float>? Queue => disposed ? null : queue;

        public RemotePlayerMovementComponent(IObjectPool<IPriorityQueue<NetworkMovementMessage, float>> queuePool)
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

        public void AddPassed(NetworkMovementMessage message, bool wasTeleported = false)
        {
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
