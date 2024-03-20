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

        public FullMovementMessage PastMessage;

        public bool Initialized;
        public bool WasTeleported;

        public readonly SimplePriorityQueue<FullMovementMessage> Queue
        {
            get
            {
                if (disposed)
                    throw new ObjectDisposedException("RemotePlayerMovementComponent");

                return queue;
            }
        }

        public RemotePlayerMovementComponent(string playerWalletId, IObjectPool<SimplePriorityQueue<FullMovementMessage>> queuePool)
        {
            PlayerWalletId = playerWalletId;
            this.queuePool = queuePool;
            queue = queuePool.Get()!;
            disposed = false;

            PastMessage = new FullMovementMessage();
            Initialized = false;
            WasTeleported = false;
        }

        public void AddPassed(FullMovementMessage message, bool wasTeleported = false)
        {
            PastMessage = message;
            WasTeleported = wasTeleported;
        }

        public void Dispose()
        {
            disposed = true;
            queuePool.Release(queue);
        }
    }
}
