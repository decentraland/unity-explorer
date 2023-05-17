using CRDT.Deserializer;
using CRDT.Protocol;
using CRDT.Protocol.Factory;
using CRDT.Serializer;
using CrdtEcsBridge.OutgoingMessages;
using CrdtEcsBridge.PoolsProviders;
using CrdtEcsBridge.WorldSynchronizer;
using Cysharp.Threading.Tasks;
using SceneRuntime.Apis.Modules;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine.Profiling;

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

        private byte[] lastSerializationBuffer;

        private readonly CustomSampler deserializeBatchSampler;
        private readonly CustomSampler worldSyncBufferSampler;
        private readonly CustomSampler outgoingMessagesSampler;

        public EngineAPIImplementation(
            ISharedPoolsProvider poolsProvider,
            IInstancePoolsProvider instancePoolsProvider,
            ICRDTProtocol crdtProtocol,
            ICRDTDeserializer crdtDeserializer,
            ICRDTSerializer crdtSerializer,
            ICRDTWorldSynchronizer crdtWorldSynchronizer,
            IOutgoingCRTDMessagesProvider outgoingCrtdMessagesProvider)
        {
            sharedPoolsProvider = poolsProvider;
            this.instancePoolsProvider = instancePoolsProvider;
            this.crdtProtocol = crdtProtocol;
            this.crdtDeserializer = crdtDeserializer;
            this.crdtSerializer = crdtSerializer;
            this.crdtWorldSynchronizer = crdtWorldSynchronizer;
            this.outgoingCrtdMessagesProvider = outgoingCrtdMessagesProvider;

            deserializeBatchSampler = CustomSampler.Create("DeserializeBatch");
            worldSyncBufferSampler = CustomSampler.Create("WorldSyncBuffer");
            outgoingMessagesSampler = CustomSampler.Create("OutgoingMessages");
        }

        public byte[] CrdtSendToRenderer(ReadOnlyMemory<byte> dataMemory)
        {
            // Called on the thread where the Scene Runtime is running (background thread)

            ReleaseSerializationBuffer();

            // Deserialize messages from the byte array
            IList<CRDTMessage> messages = instancePoolsProvider.GetDeserializationMessagesPool();

            deserializeBatchSampler.Begin();

            crdtDeserializer.DeserializeBatch(ref dataMemory, messages);

            deserializeBatchSampler.End();

            worldSyncBufferSampler.Begin();

            // as we no longer wait for a buffer to apply the thread should be frozen
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

            worldSyncBufferSampler.End();

            // Return messages to the pool before switching to the main thread,
            // it is a must because CRDT_MESSAGES_POOL is ThreadLocal
            instancePoolsProvider.ReleaseDeserializationMessagesPool(messages);

            outgoingMessagesSampler.Begin();

            // before returning to the main thread serialize outgoing CRDT Messages
            using (var outgoingMessagesSyncBlock = outgoingCrtdMessagesProvider.GetSerializationSyncBlock())
            {
                lastSerializationBuffer = sharedPoolsProvider.GetSerializedStateBytesPool(outgoingMessagesSyncBlock.GetPayloadLength());
                SerializeOutgoingCRDTMessages(outgoingMessagesSyncBlock.Messages, lastSerializationBuffer.AsSpan());
            }

            outgoingMessagesSampler.End();

            // Now (unless we make ECS run on the background thread) we need to switch to the main thread
            // before all systems start to update - in the beginning of the Player Loop
            // TODO Validate if we can just launch and forget() without waiting

            // don't use `UniTask` as it will switch us to the main thread and the continuation will keep running on the main thread
            ApplySyncCommandBuffer(worldSyncBuffer).Forget();

            return lastSerializationBuffer;
        }

        private async UniTaskVoid ApplySyncCommandBuffer(IWorldSyncCommandBuffer worldSyncBuffer)
        {
            await UniTask.Yield(PlayerLoopTiming.Initialization);

            // Apply changes to the ECS World on the main thread
            crdtWorldSynchronizer.ApplySyncCommandBuffer(worldSyncBuffer);
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

        public byte[] CrdtGetState()
        {
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
            return lastSerializationBuffer;
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
