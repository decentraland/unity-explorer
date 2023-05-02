using System.Runtime.InteropServices;

namespace CRDT.Protocol
{
    [StructLayout(LayoutKind.Sequential)]
    public readonly struct CRDTReconciliationResult
    {
        public readonly CRDTStateReconciliationResult State;
        public readonly CRDTReconciliationEffect Effect;

        public CRDTReconciliationResult(CRDTStateReconciliationResult state, CRDTReconciliationEffect effect)
        {
            State = state;
            Effect = effect;
        }

        public override string ToString() =>
            $"({State}, {Effect})";
    }
}
