using CRDT.Protocol;
using System;
using System.Collections.Generic;

namespace CRDT.Deserializer
{
    /// <summary>
    ///     CRDT Deserializer acts as a thread-safe singleton:
    ///     it does not contain a state and does not rely on pools under the hood
    /// </summary>
    public interface ICRDTDeserializer
    {
        /// <summary>
        ///     Deserializes a batch of messages into individual <see cref="CRDTMessage" />s
        /// </summary>
        /// <param name="memory">An original byte array to parse messages from</param>
        /// <param name="messages">The target list to fit all messages</param>
        void DeserializeBatch(ref ReadOnlyMemory<byte> memory, IList<CRDTMessage> messages);
    }
}
