using CRDT.Protocol;
using CrdtEcsBridge.Components;

namespace CrdtEcsBridge.WorldSynchronizer
{
    internal class BatchState
    {
        internal CRDTMessage crdtMessage;
        internal ReconciliationState reconciliationState;
        internal SDKComponentBridge sdkComponentBridge;

        internal object deserializationTarget;
    }
}
