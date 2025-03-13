using CRDT;

namespace CrdtEcsBridge.OutgoingMessages
{
    internal readonly struct OutgoingMessageKey
    {
        public readonly CRDTEntity Entity;
        public readonly int ComponentId;

        public OutgoingMessageKey(CRDTEntity entity, int componentId)
        {
            Entity = entity;
            ComponentId = componentId;
        }
    }
}
