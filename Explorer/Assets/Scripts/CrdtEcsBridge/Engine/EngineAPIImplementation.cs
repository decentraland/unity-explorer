using CRDT.Deserializer;
using CRDT.Protocol;
using CRDT.Protocol.Factory;
using CRDT.Serializer;
using CrdtEcsBridge.OutgoingMessages;
using CrdtEcsBridge.UpdateGate;
using CrdtEcsBridge.WorldSynchronizer;
using DCL.Diagnostics;
using SceneRunner.Scene.ExceptionsHandling;
using SceneRuntime.Apis.Modules;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine.Profiling;
using Utility.Multithreading;

namespace CrdtEcsBridge.Engine
{
    /// <summary>
    ///     Unique instance for each Scene Runtime
    /// </summary>
    public class EngineAPIImplementation : IEngineApi
    {
        private readonly CustomSampler applyBufferSampler;
        private readonly ICRDTDeserializer crdtDeserializer;
        private readonly CustomSampler crdtProcessMessagesSampler;

        private readonly ICRDTProtocol crdtProtocol;
        private readonly ICRDTSerializer crdtSerializer;
        private readonly ICRDTWorldSynchronizer crdtWorldSynchronizer;

        private readonly CustomSampler deserializeBatchSampler;
        private readonly ISceneExceptionsHandler exceptionsHandler;
        private readonly IInstancePoolsProvider instancePoolsProvider;
        private readonly MutexSync mutexSync;
        private readonly IOutgoingCRDTMessagesProvider outgoingCrtdMessagesProvider;
        private readonly CustomSampler outgoingMessagesSampler;
        private readonly ISharedPoolsProvider sharedPoolsProvider;
        private readonly ISystemGroupsUpdateGate systemGroupsUpdateGate;
        private readonly CustomSampler worldSyncBufferSampler;
        private bool isDisposing;

        private byte[] lastSerializationBuffer;

        public EngineAPIImplementation(
            ISharedPoolsProvider poolsProvider,
            IInstancePoolsProvider instancePoolsProvider,
            ICRDTProtocol crdtProtocol,
            ICRDTDeserializer crdtDeserializer,
            ICRDTSerializer crdtSerializer,
            ICRDTWorldSynchronizer crdtWorldSynchronizer,
            IOutgoingCRDTMessagesProvider outgoingCrtdMessagesProvider,
            ISystemGroupsUpdateGate systemGroupsUpdateGate,
            ISceneExceptionsHandler exceptionsHandler,
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
            this.systemGroupsUpdateGate = systemGroupsUpdateGate;
            this.exceptionsHandler = exceptionsHandler;

            deserializeBatchSampler = CustomSampler.Create("DeserializeBatch");
            worldSyncBufferSampler = CustomSampler.Create("WorldSyncBuffer");
            outgoingMessagesSampler = CustomSampler.Create("OutgoingMessages");
            crdtProcessMessagesSampler = CustomSampler.Create("CRDTProcessMessage");
            applyBufferSampler = CustomSampler.Create(nameof(ApplySyncCommandBuffer));
        }

        public void Dispose()
        {
            ReleaseSerializationBuffer();
            systemGroupsUpdateGate.Dispose();
        }

        public ArraySegment<byte> CrdtSendToRenderer(ReadOnlyMemory<byte> dataMemory, bool returnData = true)
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
            IWorldSyncCommandBuffer worldSyncBuffer = crdtWorldSynchronizer.GetSyncCommandBuffer();

            // Reconcile CRDT state
            for (var i = 0; i < messages.Count; i++)
            {
                crdtProcessMessagesSampler.Begin();

                CRDTMessage message = messages[i];
                CRDTReconciliationResult reconciliationResult = crdtProtocol.ProcessMessage(in message);

                crdtProcessMessagesSampler.End();

                // TODO add metric to understand how many conflicts we have based on CRDTStateReconciliationResult

                // Prepare the message to be synced with the ECS World
                worldSyncBuffer.SyncCRDTMessage(in message, reconciliationResult.Effect);
            }

            // Deserialize messages
            worldSyncBuffer.FinalizeAndDeserialize();

            worldSyncBufferSampler.End();

            instancePoolsProvider.ReleaseDeserializationMessagesPool(messages);

            ApplySyncCommandBuffer(worldSyncBuffer);

            if (returnData)
            {
                int payloadLength = SerializeOutgoingCRDTMessages();
                return new ArraySegment<byte>(lastSerializationBuffer, 0, payloadLength);
            }

            return ArraySegment<byte>.Empty;
        }

        public ArraySegment<byte> CrdtGetState()
        {
            if (isDisposing) return Array.Empty<byte>();

            Profiler.BeginThreadProfiling("SceneRuntime", "CrtdGetState");

            // Invoked on the background thread
            // this method is called rarely but the memory impact is significant

            ReleaseSerializationBuffer();

            try
            {
                // Create CRDT Messages from the current state
                // we know exactly how big the array should be
                int messagesCount = crdtProtocol.GetMessagesCount();
                ProcessedCRDTMessage[] processedMessages = sharedPoolsProvider.GetSerializationCrdtMessagesPool(messagesCount);

                int currentStatePayloadLength = crdtProtocol.CreateMessagesFromTheCurrentState(processedMessages);

                // Sync block ensures that no messages are added while they are being processed
                // By the end of the block messages are flushed and adding is unblocked
                using OutgoingCRDTMessagesSyncBlock outgoingMessagesSyncBlock = outgoingCrtdMessagesProvider.GetSerializationSyncBlock();

                int outgoingCRDTMessagesPayloadLength = outgoingMessagesSyncBlock.PayloadLength;

                int totalPayloadLength = currentStatePayloadLength + outgoingCRDTMessagesPayloadLength;

                // We know exactly how many bytes we need to serialize
                lastSerializationBuffer = sharedPoolsProvider.GetSerializedStateBytesPool(totalPayloadLength);

                // Serialize the current state
                Span<byte> currentStateSpan = lastSerializationBuffer.AsSpan()[..currentStatePayloadLength];

                for (var i = 0; i < messagesCount; i++)
                    crdtSerializer.Serialize(ref currentStateSpan, in processedMessages[i]);

                // Messages are serialized, we no longer need them in the managed form
                sharedPoolsProvider.ReleaseSerializationCrdtMessagesPool(processedMessages);

                // Serialize outgoing messages
                SerializeOutgoingCRDTMessages(outgoingMessagesSyncBlock.Messages, lastSerializationBuffer.AsSpan().Slice(currentStatePayloadLength, outgoingCRDTMessagesPayloadLength));

                // Return the buffer to the caller
                return new ArraySegment<byte>(lastSerializationBuffer, 0, totalPayloadLength);
            }
            catch (Exception e)
            {
                exceptionsHandler.OnEngineException(e, ReportCategory.CRDT);
                return Array.Empty<byte>();
            }
            finally { Profiler.EndThreadProfiling(); }
        }

        public void SetIsDisposing()
        {
            isDisposing = true;
        }

        private int SerializeOutgoingCRDTMessages()
        {
            try
            {
                outgoingMessagesSampler.Begin();

                int payloadLength;

                using (OutgoingCRDTMessagesSyncBlock outgoingMessagesSyncBlock = outgoingCrtdMessagesProvider.GetSerializationSyncBlock())
                {
                    lastSerializationBuffer =
                        sharedPoolsProvider.GetSerializedStateBytesPool(
                            payloadLength = outgoingMessagesSyncBlock.PayloadLength);

                    SerializeOutgoingCRDTMessages(outgoingMessagesSyncBlock.Messages, lastSerializationBuffer.AsSpan());

                    SyncOutgoingCRDTMessages(outgoingMessagesSyncBlock.Messages);
                }

                outgoingMessagesSampler.End();
                return payloadLength;
            }
            catch (Exception e)
            {
                exceptionsHandler.OnEngineException(e, ReportCategory.CRDT);
                return 0;
            }
        }

        /// <summary>
        ///     If local messages are not written in the local CRDT, the final state
        ///     including timestamp is incorrect. Subsequent creation of propagated messages will result in a wrong timestamp
        /// </summary>
        private void SyncOutgoingCRDTMessages(IReadOnlyList<ProcessedCRDTMessage> outgoingMessages)
        {
            for (var i = 0; i < outgoingMessages.Count; i++)
            {
                ProcessedCRDTMessage m = outgoingMessages[i];

                // We are interested in LWW messages only,
                switch (m.message.Type)
                {
                    case CRDTMessageType.DELETE_ENTITY:
                    case CRDTMessageType.PUT_COMPONENT:
                        // instead of processing via CRDTProtocol.ProcessMessage
                        // we can skip part of the logic as we guarantee that the local message is the final valid state (see OutgoingCRDTMessagesProvider.AddLwwMessage)
                        crdtProtocol.UpdateLWWState(m.message);
                        break;
                    default:
                        // as this data is not kept in CRDTProtocol it must be released immediately
                        m.message.Data.Dispose();
                        break;
                }
            }
        }

        // Use mutex to apply command buffer from the background thread instead of synchronizing by the main one
        private void ApplySyncCommandBuffer(IWorldSyncCommandBuffer worldSyncBuffer)
        {
            try
            {
                using MutexSync.Scope mutex = mutexSync.GetScope();

                applyBufferSampler.Begin();

                // Apply changes to the ECS World on the main thread
                crdtWorldSynchronizer.ApplySyncCommandBuffer(worldSyncBuffer);
                applyBufferSampler.End();

                // Allow system for which throttling is enabled to process once
                // If the scene is updated more frequently than Unity Loop the gate will be effectively open all the time
                systemGroupsUpdateGate.Open();
            }
            catch (Exception e) { exceptionsHandler.OnEngineException(e, ReportCategory.CRDT_ECS_BRIDGE); }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void SerializeOutgoingCRDTMessages(IReadOnlyCollection<ProcessedCRDTMessage> outgoingMessages, Span<byte> span)
        {
            if (outgoingMessages.Count == 0) return;

            foreach (ProcessedCRDTMessage processedCRDTMessage in outgoingMessages)
                crdtSerializer.Serialize(ref span, in processedCRDTMessage);
        }

        /// <summary>
        ///     When the state or outgoing messages processed by the Scene Runtime we can safely return them to the pool.
        ///     It is guaranteed by the sequential order of `CrdtSendToRenderer`/`CrdtGetState` calls
        /// </summary>
        private void ReleaseSerializationBuffer()
        {
            if (lastSerializationBuffer != null)
            {
                sharedPoolsProvider.ReleaseSerializedStateBytesPool(lastSerializationBuffer);
                lastSerializationBuffer = null;
            }
        }
    }
}
