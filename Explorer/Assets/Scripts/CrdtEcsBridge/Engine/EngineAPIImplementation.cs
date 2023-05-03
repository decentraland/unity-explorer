using CRDT.Deserializer;
using CRDT.Protocol;
using CRDT.Protocol.Factory;
using CRDT.Serializer;
using CrdtEcsBridge.OutgoingMessages;
using CrdtEcsBridge.WorldSynchronizer;
using Cysharp.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace CrdtEcsBridge.Engine
{
    /// <summary>
    /// Unique instance for each Scene Runtime
    /// </summary>
    public class EngineAPIImplementation : IEngineApi
    {
        private readonly IEngineAPIPoolsProvider poolsProvider;

        private readonly ICRDTProtocol crdtProtocol;
        private readonly ICRDTDeserializer crdtDeserializer;
        private readonly ICRDTSerializer crdtSerializer;
        private readonly ICrdtWorldSynchronizer crdtWorldSynchronizer;
        private readonly IOutgoingCRTDMessagesProvider outgoingCrtdMessagesProvider;

        private byte[] lastSerializationBuffer;

        public EngineAPIImplementation(IEngineAPIPoolsProvider poolsProvider, ICRDTProtocol crdtProtocol, ICRDTDeserializer crdtDeserializer, ICRDTSerializer crdtSerializer, ICrdtWorldSynchronizer crdtWorldSynchronizer,
            IOutgoingCRTDMessagesProvider outgoingCrtdMessagesProvider)
        {
            this.poolsProvider = poolsProvider;
            this.crdtProtocol = crdtProtocol;
            this.crdtDeserializer = crdtDeserializer;
            this.crdtSerializer = crdtSerializer;
            this.crdtWorldSynchronizer = crdtWorldSynchronizer;
            this.outgoingCrtdMessagesProvider = outgoingCrtdMessagesProvider;
        }

        public async UniTask<byte[]> CrdtSendToRenderer(byte[] data)
        {
            // Called on the thread where the Scene Runtime is running (background thread)

            ReleaseSerializationBuffer();

            // Deserialize messages from the byte array
            var messages = poolsProvider.GetDeserializationMessagesPool();

            ReadOnlyMemory<byte> dataMemory = data;

            // TODO add metrics to understand bottlenecks better
            crdtDeserializer.DeserializeBatch(ref dataMemory, messages);

            var worldSyncBuffer = crdtWorldSynchronizer.GetSyncCommandBuffer();

            // Reconcile CRDT state
            for (var i = 0; i < messages.Count; i++)
            {
                var message = messages[i];
                var reconciliationResult = crdtProtocol.ProcessMessage(in message);

                // TODO add metric to understand how many conflicts we have based on CRDTStateReconciliationResult

                // Prepare the message to be synced with the ECS World
                worldSyncBuffer.SyncCRDTMessage(in message, reconciliationResult.Effect);
            }

            // Deserialize messages on the main thread
            worldSyncBuffer.FinalizeAndDeserialize();

            // Return messages to the pool before switching to the main thread,
            // it is a must because CRDT_MESSAGES_POOL is ThreadLocal
            poolsProvider.ReleaseDeserializationMessagesPool(messages);

            // before returning to the main thread serialize outgoing CRDT Messages
            using (var outgoingMessagesSyncBlock = outgoingCrtdMessagesProvider.GetSerializationSyncBlock())
            {
                lastSerializationBuffer = poolsProvider.GetSerializedStateBytesPool(outgoingMessagesSyncBlock.GetPayloadLength());
                SerializeOutgoingCRDTMessages(outgoingMessagesSyncBlock.Messages, lastSerializationBuffer.AsSpan());
            }

            // Now (unless we make ECS run on the background thread) we need to switch to the main thread
            // before all systems start to update - in the beginning of the Player Loop
            // TODO Validate if we can just launch and forget() without waiting
            await UniTask.Yield(PlayerLoopTiming.Initialization);

            // Apply changes to the ECS World on the main thread
            crdtWorldSynchronizer.ApplySyncCommandBuffer(worldSyncBuffer);
            return lastSerializationBuffer;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void SerializeOutgoingCRDTMessages(IReadOnlyList<ProcessedCRDTMessage> outgoingMessages, Span<byte> span)
        {
            for (var i = 0; i < outgoingMessages.Count; i++)
            {
                var processedCRDTMessage = outgoingMessages[i];
                crdtSerializer.Serialize(ref span, in processedCRDTMessage);
            }
        }

        public UniTask<byte[]> CrdtGetState()
        {
            // Invoked on the background thread
            // this method is called rarely but the memory impact is significant

            ReleaseSerializationBuffer();

            // Create CRDT Messages from the current state
            // we know exactly how big the array should be
            ProcessedCRDTMessage[] processedMessages = poolsProvider.GetSerializationCrdtMessagesPool(crdtProtocol.GetMessagesCount());

            var currentStatePayloadLength = crdtProtocol.CreateMessagesFromTheCurrentState(processedMessages);

            // Sync block ensures that no messages are added while they are being processed
            // By the end of the block messages are flushed and adding is unblocked
            using var outgoingMessagesSyncBlock = outgoingCrtdMessagesProvider.GetSerializationSyncBlock();

            var outgoingCRDTMessagesPayloadLength = outgoingMessagesSyncBlock.GetPayloadLength();

            var totalPayloadLength = currentStatePayloadLength + outgoingCRDTMessagesPayloadLength;

            // We know exactly how many bytes we need to serialize
            lastSerializationBuffer = poolsProvider.GetSerializedStateBytesPool(totalPayloadLength);

            // Serialize the current state
            var currentStateSpan = lastSerializationBuffer.AsSpan().Slice(currentStatePayloadLength);

            for (var i = 0; i < processedMessages.Length; i++)
                crdtSerializer.Serialize(ref currentStateSpan, in processedMessages[i]);

            // Messages are serialized, we no longer need them in the managed form
            poolsProvider.ReleaseSerializationCrdtMessagesPool(processedMessages);

            // Serialize outgoing messages
            SerializeOutgoingCRDTMessages(outgoingMessagesSyncBlock.Messages, lastSerializationBuffer.AsSpan().Slice(currentStatePayloadLength, outgoingCRDTMessagesPayloadLength));

            // Return the buffer to the caller
            return UniTask.FromResult(lastSerializationBuffer);
        }

        /// <summary>
        /// When the state or outgoing messages processed by the Scene Runtime we can safely return them to the pool.
        /// It is guaranteed by the sequential order of `CrdtSendToRenderer`/`CrdtGetState` calls
        /// </summary>
        private void ReleaseSerializationBuffer()
        {
            if (lastSerializationBuffer != null)
            {
                poolsProvider.ReleaseSerializedStateBytesPool(lastSerializationBuffer);
                lastSerializationBuffer = null;
            }
        }

        public void Dispose()
        {
            ReleaseSerializationBuffer();
        }
    }
}
