namespace CRDT.Protocol
{
    /// <summary>
    ///     Contains the arithmetic sum of the fields' size which compound different CRDT messages
    ///     in their serialized form
    /// </summary>
    public static class CRDTConstants
    {
        public const int MESSAGE_HEADER_LENGTH = 8;

        public const int CRDT_PUT_COMPONENT_HEADER_LENGTH = 16;
        public const int CRDT_DELETE_COMPONENT_HEADER_LENGTH = 12;
        public const int CRDT_DELETE_ENTITY_HEADER_LENGTH = 4;
        public const int CRDT_APPEND_COMPONENT_HEADER_LENGTH = 16;

        public const int CRDT_PUT_COMPONENT_BASE_LENGTH = MESSAGE_HEADER_LENGTH + CRDT_PUT_COMPONENT_HEADER_LENGTH;
        public const int CRDT_DELETE_COMPONENT_BASE_LENGTH = MESSAGE_HEADER_LENGTH + CRDT_DELETE_COMPONENT_HEADER_LENGTH;
        public const int CRDT_DELETE_ENTITY_BASE_LENGTH = MESSAGE_HEADER_LENGTH + CRDT_DELETE_ENTITY_HEADER_LENGTH;
        public const int CRDT_APPEND_COMPONENT_BASE_LENGTH = MESSAGE_HEADER_LENGTH + CRDT_APPEND_COMPONENT_HEADER_LENGTH;
    }
}
