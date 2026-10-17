using CRDT.Protocol;
using System;

namespace CRDT.CRDTTests.Protocol
{
    [Serializable]
    internal struct CRDTTestMessageResult
    {
        public CRDTStateReconciliationResult state;
        public CRDTReconciliationEffect effect;

        public CRDTReconciliationResult ToCRDTReconciliationResult() =>
            new (state, effect);
    }
}
