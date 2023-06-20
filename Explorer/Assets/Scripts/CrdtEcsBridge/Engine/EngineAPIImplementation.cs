using CRDT.Deserializer;
using CRDT.Protocol;
using CRDT.Protocol.Factory;
using CRDT.Serializer;
using CrdtEcsBridge.OutgoingMessages;
using CrdtEcsBridge.WorldSynchronizer;
using SceneRuntime.Apis.Modules;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine.Profiling;
using Utility.Multithreading;

namespace CrdtEcsBridge.Engine
{
    /// <summary>
    /// Unique instance for each Scene Runtime
    /// </summary>
    public class EngineAPIImplementation : IEngineApi
    {
        private readonly ISharedPoolsProvider sharedPoolsProvider;
        private readonly IInstancePoolsProvider instancePoolsProvider;

        private readonly ICRDTProtocol crdtProtocol;
        private readonly ICRDTDeserializer crdtDeserializer;
        private readonly ICRDTSerializer crdtSerializer;
        private readonly ICRDTWorldSynchronizer crdtWorldSynchronizer;
        private readonly IOutgoingCRTDMessagesProvider outgoingCrtdMessagesProvider;
        private readonly MutexSync mutexSync;

        private byte[] lastSerializationBuffer;

        private readonly CustomSampler deserializeBatchSampler;
        private readonly CustomSampler worldSyncBufferSampler;
        private readonly CustomSampler outgoingMessagesSampler;
        private readonly CustomSampler crdtProcessMessagesSampler;
        private readonly CustomSampler applyBufferSampler;

        private bool isDisposing;

        public EngineAPIImplementation(
            ISharedPoolsProvider poolsProvider,
            IInstancePoolsProvider instancePoolsProvider,
            ICRDTProtocol crdtProtocol,
            ICRDTDeserializer crdtDeserializer,
            ICRDTSerializer crdtSerializer,
            ICRDTWorldSynchronizer crdtWorldSynchronizer,
            IOutgoingCRTDMessagesProvider outgoingCrtdMessagesProvider,
            MutexSync mutexSync)
        {
            sharedPoolsProvider = poolsProvider;
            this.instancePoolsProvider = instancePoolsProvider;
            this.crdtProtocol = crdtProtocol;
            this.crdtDeserializer = crdtDeserializer;
            this.crdtSerializer = crdtSerializer;
            this.crdtWorldSynchronizer = crdtWorldSynchronizer;
            this.outgoingCrtdMessagesProvider = outgoingCrtdMessagesProvider;
            this.mutexSync = mutexSync;

            deserializeBatchSampler = CustomSampler.Create("DeserializeBatch");
            worldSyncBufferSampler = CustomSampler.Create("WorldSyncBuffer");
            outgoingMessagesSampler = CustomSampler.Create("OutgoingMessages");
            crdtProcessMessagesSampler = CustomSampler.Create("CRDTProcessMessage");
            applyBufferSampler = CustomSampler.Create(nameof(ApplySyncCommandBuffer));
        }

        public ArraySegment<byte> CrdtSendToRenderer(ReadOnlyMemory<byte> dataMemory)
        {
            // TODO it's dirty, think how to do it better
            if (isDisposing) return Array.Empty<byte>();

            // Called on the thread where the Scene Runtime is running (background thread)

            ReleaseSerializationBuffer();

            // Deserialize messages from the byte array
            List<CRDTMessage> messages = instancePoolsProvider.GetDeserializationMessagesPool();

            deserializeBatchSampler.Begin();

            // TODO add metrics to understand bottlenecks better
            crdtDeserializer.DeserializeBatch(ref dataMemory, messages);

            deserializeBatchSampler.End();

            worldSyncBufferSampler.Begin();

            // as we no longer wait for a buffer to apply the thread should be frozen
            var worldSyncBuffer = crdtWorldSynchronizer.GetSyncCommandBuffer();

            // Reconcile CRDT state
            for (var i = 0; i < messages.Count; i++)
            {
                crdtProcessMessagesSampler.Begin();

                var message = messages[i];
                var reconciliationResult = crdtProtocol.ProcessMessage(in message);

                crdtProcessMessagesSampler.End();

                // TODO add metric to understand how many conflicts we have based on CRDTStateReconciliationResult

                // Prepare the message to be synced with the ECS World
                worldSyncBuffer.SyncCRDTMessage(in message, reconciliationResult.Effect);
            }

            // Deserialize messages on the main thread
            worldSyncBuffer.FinalizeAndDeserialize();

            worldSyncBufferSampler.End();

            // Return messages to the pool before switching to the main thread,
            // it is a must because CRDT_MESSAGES_POOL is ThreadLocal
            instancePoolsProvider.ReleaseDeserializationMessagesPool(messages);

            outgoingMessagesSampler.Begin();

            int payloadLength;

            // before returning to the main thread serialize outgoing CRDT Messages
            using (var outgoingMessagesSyncBlock = outgoingCrtdMessagesProvider.GetSerializationSyncBlock())
            {
                lastSerializationBuffer = sharedPoolsProvider.GetSerializedStateBytesPool(payloadLength = outgoingMessagesSyncBlock.GetPayloadLength());
                SerializeOutgoingCRDTMessages(outgoingMessagesSyncBlock.Messages, lastSerializationBuffer.AsSpan());
            }

            outgoingMessagesSampler.End();

            ApplySyncCommandBuffer(worldSyncBuffer);

            return new ArraySegment<byte>(lastSerializationBuffer, 0, payloadLength);
        }

        // Use mutex to apply command buffer from the background thread instead of synchronizing by the main one
        private void ApplySyncCommandBuffer(IWorldSyncCommandBuffer worldSyncBuffer)
        {
            using MutexSync.Scope mutex = mutexSync.GetScope();

            applyBufferSampler.Begin();

            // Apply changes to the ECS World on the main thread
            crdtWorldSynchronizer.ApplySyncCommandBuffer(worldSyncBuffer);
            applyBufferSampler.End();
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

        public ArraySegment<byte> CrdtGetState()
        {
            // TODO it's dirty, think how to do it better
            if (isDisposing) return Array.Empty<byte>();

            Profiler.BeginThreadProfiling("SceneRuntime", "CrtdGetState");

            // Invoked on the background thread
            // this method is called rarely but the memory impact is significant

            ReleaseSerializationBuffer();

            // Create CRDT Messages from the current state
            // we know exactly how big the array should be
            ProcessedCRDTMessage[] processedMessages = sharedPoolsProvider.GetSerializationCrdtMessagesPool(crdtProtocol.GetMessagesCount());

            var currentStatePayloadLength = crdtProtocol.CreateMessagesFromTheCurrentState(processedMessages);

            // Sync block ensures that no messages are added while they are being processed
            // By the end of the block messages are flushed and adding is unblocked
            using var outgoingMessagesSyncBlock = outgoingCrtdMessagesProvider.GetSerializationSyncBlock();

            var outgoingCRDTMessagesPayloadLength = outgoingMessagesSyncBlock.GetPayloadLength();

            var totalPayloadLength = currentStatePayloadLength + outgoingCRDTMessagesPayloadLength;

            // We know exactly how many bytes we need to serialize
            lastSerializationBuffer = sharedPoolsProvider.GetSerializedStateBytesPool(totalPayloadLength);

            // Serialize the current state
            var currentStateSpan = lastSerializationBuffer.AsSpan().Slice(currentStatePayloadLength);

            for (var i = 0; i < processedMessages.Length; i++)
                crdtSerializer.Serialize(ref currentStateSpan, in processedMessages[i]);

            // Messages are serialized, we no longer need them in the managed form
            sharedPoolsProvider.ReleaseSerializationCrdtMessagesPool(processedMessages);

            // Serialize outgoing messages
            SerializeOutgoingCRDTMessages(outgoingMessagesSyncBlock.Messages, lastSerializationBuffer.AsSpan().Slice(currentStatePayloadLength, outgoingCRDTMessagesPayloadLength));

            Profiler.EndThreadProfiling();

            // Return the buffer to the caller
            return new ArraySegment<byte>(lastSerializationBuffer, 0, totalPayloadLength);
        }

        public void SetIsDisposing()
        {
            isDisposing = true;
        }

        /// <summary>
        /// When the state or outgoing messages processed by the Scene Runtime we can safely return them to the pool.
        /// It is guaranteed by the sequential order of `CrdtSendToRenderer`/`CrdtGetState` calls
        /// </summary>
        private void ReleaseSerializationBuffer()
        {
            if (lastSerializationBuffer != null)
            {
                sharedPoolsProvider.ReleaseSerializedStateBytesPool(lastSerializationBuffer);
                lastSerializationBuffer = null;
            }
        }

        public void Dispose()
        {
            ReleaseSerializationBuffer();
        }
    }
}
