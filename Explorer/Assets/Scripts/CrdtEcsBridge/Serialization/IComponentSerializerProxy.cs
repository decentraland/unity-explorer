using System;

namespace CrdtEcsBridge.Serialization
{
    /// <summary>
    /// An entry point for communication components serialization and deserialization.
    /// </summary>
    public interface IComponentSerializerProxy
    {
        /// <summary>
        /// Deserializes component that was retrieved after CRDT Reconciliation
        /// </summary>
        /// <param name="componentType">Must be a reference type</param>
        /// <param name="data">byte stream</param>
        /// <param name="deserializationBuffer">Buffer to put the result in, should be big enough to fit one more component</param>
        void DeserializeProtocolComponent(Type componentType, in ReadOnlySpan<byte> data, ref DeserializationBuffer deserializationBuffer);
    }
}
