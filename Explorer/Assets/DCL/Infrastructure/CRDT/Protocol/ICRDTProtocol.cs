using CRDT.Protocol.Factory;
using System;
using System.Buffers;

namespace CRDT.Protocol
{
    /// <summary>
    ///     CRDT Protocol instance corresponds to each individual instance of Scene Runner.
    ///     It can be run from the background thread, it is not thread-safe but thread-agnostic
    /// </summary>
    public interface ICRDTProtocol : IDisposable
    {
        /// <summary>
        ///     <inheritdoc cref="CRDTProtocol.State.messagesCount" />
        /// </summary>
        int GetMessagesCount();

        CRDTReconciliationResult ProcessMessage(in CRDTMessage message);

        /// <summary>
        ///     Enforce LWW state update, it must be guaranteed that the message is PUT_COMPONENT or DELETE_COMPONENT
        /// </summary>
        void EnforceLWWState(in CRDTMessage message);

        /// <summary>
        ///     <inheritdoc cref="CRDTMessagesFactory.CreateMessagesFromTheCurrentState" />
        /// </summary>
        /// <returns>
        ///     <inheritdoc cref="CRDTMessagesFactory.CreateMessagesFromTheCurrentState" />
        /// </returns>
        int CreateMessagesFromTheCurrentState(ProcessedCRDTMessage[] preallocatedArray);

        /// <summary>
        ///     Creates an Append Message but does not process it
        /// </summary>
        ProcessedCRDTMessage CreateAppendMessage(CRDTEntity entity, int componentId, int timestamp, in IMemoryOwner<byte> data);

        /// <summary>
        ///     Creates an LWW PUT Message but does not process it
        /// </summary>
        ProcessedCRDTMessage CreatePutMessage(CRDTEntity entity, int componentId, in IMemoryOwner<byte> data);

        /// <summary>
        ///     Creates an LWW DELETE Message but does not process it
        /// </summary>
        ProcessedCRDTMessage CreateDeleteMessage(CRDTEntity entity, int componentId);
    }
}
