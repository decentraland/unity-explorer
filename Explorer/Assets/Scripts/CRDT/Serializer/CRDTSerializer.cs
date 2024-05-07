using CRDT.Protocol;
using CRDT.Protocol.Factory;
using System;
using Utility;

namespace CRDT.Serializer
{
    public class CRDTSerializer : ICRDTSerializer
    {
        public void Serialize(ref Span<byte> destination, in ProcessedCRDTMessage processedMessage)
        {
            processedMessage.LogSelf(nameof(CRDTSerializer));
            CRDTMessage crdtMessage = processedMessage.message;

            destination.Write(processedMessage.CRDTMessageDataLength);
            destination.WriteEnumAs<CRDTMessageType, int>(crdtMessage.Type);
            destination.Write(crdtMessage.EntityId);

            switch (crdtMessage.Type)
            {
                case CRDTMessageType.PUT_COMPONENT:
                case CRDTMessageType.APPEND_COMPONENT:
                    destination.Write(crdtMessage.ComponentId);
                    destination.Write(crdtMessage.Timestamp);
                    destination.Write(crdtMessage.Data.Memory.Length);
                    destination.Write(crdtMessage.Data.Memory.Span);
                    break;
                case CRDTMessageType.DELETE_COMPONENT:
                    destination.Write(crdtMessage.ComponentId);
                    destination.Write(crdtMessage.Timestamp);
                    break;
            }

            // for DELETE_ENTITY no additional data required
        }
    }
}
