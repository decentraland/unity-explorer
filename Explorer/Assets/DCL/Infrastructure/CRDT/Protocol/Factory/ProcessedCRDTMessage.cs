using System.Runtime.InteropServices;

namespace CRDT.Protocol.Factory
{
    /// <summary>
    ///     Contains the size field denoting the number of bytes required for Serialization
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public readonly struct ProcessedCRDTMessage
    {
        public readonly CRDTMessage message;
        public readonly int CRDTMessageDataLength;

        public ProcessedCRDTMessage(CRDTMessage message, int crdtMessageDataLength)
        {
            this.message = message;
            CRDTMessageDataLength = crdtMessageDataLength;
        }
    }
}
