using CRDT.Protocol;
using CrdtEcsBridge.Components;
using DCL.Optimization.Pools;
using DCL.Optimization.ThreadSafePool;

namespace CrdtEcsBridge.WorldSynchronizer
{
    internal class BatchState
    {
        internal static readonly ThreadSafeObjectPool<BatchState> POOL = new (
            () => new BatchState(),
            actionOnRelease: state => state.deserializationTarget = null,

            // Omit checking collections, it is a hot path on the main thread
            collectionCheck: PoolConstants.CHECK_COLLECTIONS);

        internal CRDTMessage crdtMessage;
        internal ReconciliationState reconciliationState;
        internal SDKComponentBridge sdkComponentBridge;

        internal object deserializationTarget;

        private BatchState() { }
    }
}
