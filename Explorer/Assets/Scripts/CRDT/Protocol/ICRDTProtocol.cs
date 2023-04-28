using CRDT.Protocol.Factory;
using System;

namespace CRDT.Protocol
{
    /// <summary>
    /// CRDT Protocol instance corresponds to each individual instance of Scene Runner.
    /// It can be run from the background thread, it is not thread-safe but thread-agnostic
    /// </summary>
    public interface ICRDTProtocol : IDisposable
    {
        /// <summary>
        /// <inheritdoc cref="CRDTProtocol.State.messagesCount"/>
        /// </summary>
        int GetMessagesCount();

        ProcessMessageResult ProcessMessage(in CRDTMessage message);

        /// <summary>
        /// <inheritdoc cref="CRDTMessagesFactory.CreateMessagesFromTheCurrentState"/>
        /// </summary>
        /// <returns><inheritdoc cref="CRDTMessagesFactory.CreateMessagesFromTheCurrentState"/></returns>
        int CreateMessagesFromTheCurrentState(ProcessedCRDTMessage[] preallocatedArray);

        /// <summary>
        /// Creates an Append Message but does not process it
        /// </summary>
        ProcessedCRDTMessage CreateAppendMessage(CRDTEntity entity, int componentId, in ReadOnlyMemory<byte> data);

        /// <summary>
        /// Creates an LWW PUT Message but does not process it
        /// </summary>
        ProcessedCRDTMessage CreatePutMessage(CRDTEntity entity, int componentId, in ReadOnlyMemory<byte> data);

        /// <summary>
        /// Creates an LWW DELETE Message but does not process it
        /// </summary>
        ProcessedCRDTMessage CreateDeleteMessage(CRDTEntity entity, int componentId);
    }
}
