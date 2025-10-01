// ReSharper disable InconsistentNaming
namespace CRDT.Protocol
{
    public enum CRDTMessageType : byte
    {
        NONE = 0,

        // The last component per type is valid, others can be discarded
        PUT_COMPONENT = 1,
        DELETE_COMPONENT = 2,
        DELETE_ENTITY = 3,

        // All components are valid and must be processed
        APPEND_COMPONENT = 4,

        // Network
        PUT_COMPONENT_NETWORK = 5,
        DELETE_COMPONENT_NETWORK = 6,
        DELETE_ENTITY_NETWORK = 7,

        MAX_MESSAGE_TYPE,
    }
}
