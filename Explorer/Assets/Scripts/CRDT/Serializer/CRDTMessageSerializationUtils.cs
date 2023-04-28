using CRDT.Protocol;
using System;

namespace CRDT.Serializer
{
    internal static class CRDTMessageSerializationUtils
    {
        internal static int GetMessageDataLength(in this CRDTMessage message)
        {
            switch (message.Type)
            {
                case CRDTMessageType.PUT_COMPONENT:
                    return CRDTConstants.CRDT_PUT_COMPONENT_BASE_LENGTH + message.Data.Length;
                case CRDTMessageType.DELETE_ENTITY:
                    return CRDTConstants.CRDT_DELETE_ENTITY_BASE_LENGTH;
                case CRDTMessageType.DELETE_COMPONENT:
                    return CRDTConstants.CRDT_DELETE_COMPONENT_BASE_LENGTH;
                case CRDTMessageType.APPEND_COMPONENT:
                    return CRDTConstants.CRDT_APPEND_COMPONENT_BASE_LENGTH + message.Data.Length;
            }

            return 0;
        }

        internal static int GetMessageDataLength(CRDTMessageType messageType, in ReadOnlyMemory<byte> data)
        {
            switch (messageType)
            {
                case CRDTMessageType.PUT_COMPONENT:
                    return CRDTConstants.CRDT_PUT_COMPONENT_BASE_LENGTH + data.Length;
                case CRDTMessageType.DELETE_ENTITY:
                    return CRDTConstants.CRDT_DELETE_ENTITY_BASE_LENGTH;
                case CRDTMessageType.DELETE_COMPONENT:
                    return CRDTConstants.CRDT_DELETE_COMPONENT_BASE_LENGTH;
                case CRDTMessageType.APPEND_COMPONENT:
                    return CRDTConstants.CRDT_APPEND_COMPONENT_BASE_LENGTH + data.Length;
            }

            return 0;
        }
    }
}
