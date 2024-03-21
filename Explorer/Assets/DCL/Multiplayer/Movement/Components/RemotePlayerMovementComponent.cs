#nullable enable

using System;
using UnityEngine.Pool;
using Utility.PriorityQueue;

namespace DCL.Multiplayer.Movement
{
    public struct RemotePlayerMovementComponent : IDisposable
    {
        public const string TEST_ID = "SelfReplica";

        private readonly IObjectPool<SimplePriorityQueue<FullMovementMessage>> queuePool;
        private readonly SimplePriorityQueue<FullMovementMessage> queue;
        private bool disposed;

        public readonly string PlayerWalletId;

        public NetworkMovementMessage PastMessage;

        public bool Initialized;
        public bool WasTeleported;
        public bool RequireAnimationsUpdate;

        public readonly SimplePriorityQueue<FullMovementMessage>? Queue => disposed ? null : queue;

        public RemotePlayerMovementComponent(string playerWalletId, IObjectPool<SimplePriorityQueue<FullMovementMessage>> queuePool)
        {
            PlayerWalletId = playerWalletId;
            this.queuePool = queuePool;
            queue = queuePool.Get()!;
            disposed = false;

            PastMessage = new NetworkMovementMessage();
            Initialized = false;
            WasTeleported = false;

            RequireAnimationsUpdate = false;
        }

        public void AddPassed(NetworkMovementMessage message, bool wasTeleported = false)
        {
            PastMessage = message;
            WasTeleported = wasTeleported;

            RequireAnimationsUpdate = true;
        }

        public void Dispose()
        {
            disposed = true;
            queuePool.Release(queue);
        }
    }
}
