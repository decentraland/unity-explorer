using CRDT.Deserializer;
using CRDT.Protocol;
using CRDT.Protocol.Factory;
using CRDT.Serializer;
using CrdtEcsBridge.OutgoingMessages;
using CrdtEcsBridge.PoolsProviders;
using CrdtEcsBridge.UpdateGate;
using CrdtEcsBridge.WorldSynchronizer;
using DCL.Diagnostics;
using DCL.Profiling;
using SceneRunner.Scene.ExceptionsHandling;
using SceneRuntime.Apis.Modules.EngineApi;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine.Profiling;
using Utility.Multithreading;
using Profiler = UnityEngine.Profiling.Profiler;

namespace CrdtEcsBridge.JsModulesImplementation
{
    /// <summary>
    ///     Unique instance for each Scene Runtime
    /// </summary>
    public class EngineAPIImplementation : IEngineApi
    {
        protected readonly ISharedPoolsProvider sharedPoolsProvider;
        private readonly CustomSampler applyBufferSampler;
        private readonly ICRDTDeserializer crdtDeserializer;
        private readonly CustomSampler crdtProcessMessagesSampler;

        private readonly ICRDTProtocol crdtProtocol;
        private readonly ICRDTSerializer crdtSerializer;
        private readonly ICRDTWorldSynchronizer crdtWorldSynchronizer;

        private readonly CustomSampler deserializeBatchSampler;
        private readonly ISceneExceptionsHandler exceptionsHandler;
        private readonly IInstancePoolsProvider instancePoolsProvider;
        private readonly MultiThreadSync multiThreadSync;
        private readonly MultiThreadSync.Owner syncOwner;
        private readonly IOutgoingCRDTMessagesProvider outgoingCrtdMessagesProvider;
        private readonly CustomSampler outgoingMessagesSampler;
        private readonly ISystemGroupsUpdateGate systemGroupsUpdateGate;
        private readonly CustomSampler worldSyncBufferSampler;
        private readonly SceneRuntimeMetrics metrics;

        /// <summary>
        ///     Invoked for every pending outgoing message before serialization.
        ///     Null by default so the serialization loop skips the delegate invocation entirely
        /// </summary>
        protected virtual Action<OutgoingCRDTMessagesProvider.PendingMessage>? PendingMessageProcessor => null;

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
            MultiThreadSync multiThreadSync,
            MultiThreadSync.Owner syncOwner,
            SceneRuntimeMetrics metrics)
        {
            sharedPoolsProvider = poolsProvider;
            this.instancePoolsProvider = instancePoolsProvider;
            this.crdtProtocol = crdtProtocol;
            this.crdtDeserializer = crdtDeserializer;
            this.crdtSerializer = crdtSerializer;
            this.crdtWorldSynchronizer = crdtWorldSynchronizer;
            this.outgoingCrtdMessagesProvider = outgoingCrtdMessagesProvider;
            this.multiThreadSync = multiThreadSync;
            this.syncOwner = syncOwner;
            this.systemGroupsUpdateGate = systemGroupsUpdateGate;
            this.exceptionsHandler = exceptionsHandler;
            this.metrics = metrics;

            deserializeBatchSampler = CustomSampler.Create("DeserializeBatch");
            worldSyncBufferSampler = CustomSampler.Create("WorldSyncBuffer");
            outgoingMessagesSampler = CustomSampler.Create("OutgoingMessages");
            crdtProcessMessagesSampler = CustomSampler.Create("CRDTProcessMessage");
            applyBufferSampler = CustomSampler.Create(nameof(ApplySyncCommandBuffer));
        }

        public virtual void Dispose() { }

        public PoolableByteArray CrdtSendToRenderer(ReadOnlyMemory<byte> dataMemory, bool returnData = true)
        {
            // Called on the thread where the Scene Runtime is running (background thread)

            // Deserialize messages from the byte array
            List<CRDTMessage> messages = instancePoolsProvider.GetDeserializationMessagesPool();

            deserializeBatchSampler.Begin();

            // TODO add metrics to understand bottlenecks better
            crdtDeserializer.DeserializeBatch(ref dataMemory, messages);

            deserializeBatchSampler.End();

            metrics.MessagesFromScene.Add(messages.Count);

            worldSyncBufferSampler.Begin();

            // as we no longer wait for a buffer to apply the thread should be frozen
            IWorldSyncCommandBuffer worldSyncBuffer = crdtWorldSynchronizer.GetSyncCommandBuffer();

            // Reconcile CRDT state
            for (var i = 0; i < messages.Count; i++)
            {
                CRDTMessage message = messages[i];

                // Skip Creator Hub components leaked from main.composite (inspector::*,
                // specific asset-packs:: tooling metadata, composite::root) and phantom
                // Creator Hub tags (custom components with empty data, e.g. cube-id).
                if (message.Type is CRDTMessageType.PUT_COMPONENT
                        or CRDTMessageType.DELETE_COMPONENT
                        or CRDTMessageType.APPEND_COMPONENT
                    && CreatorHubComponentFilter.ShouldFilter(in message))
                {
                    message.Data.Dispose();
                    continue;
                }

                crdtProcessMessagesSampler.Begin();

                CRDTReconciliationResult reconciliationResult = crdtProtocol.ProcessMessage(in message);

                crdtProcessMessagesSampler.End();

                // TODO add metric to understand how many conflicts we have based on CRDTStateReconciliationResult

                // Prepare the message to be synced with the ECS World
                worldSyncBuffer.SyncCRDTMessage(in message, reconciliationResult.Effect);
            }

            // Deserialize messages
            worldSyncBuffer.FinalizeAndDeserialize();

            worldSyncBufferSampler.End();

            ApplySyncCommandBuffer(worldSyncBuffer);
            instancePoolsProvider.ReleaseDeserializationMessagesPool(messages);

            return returnData ? SerializeOutgoingCRDTMessages() : PoolableByteArray.EMPTY;
        }

        public PoolableByteArray CrdtGetState()
        {
            Profiler.BeginThreadProfiling("SceneRuntime", "CrtdGetState");

            // Invoked on the background thread
            // this method is called rarely but the memory impact is significant

            try
            {
                // Drain the outgoing messages so they are reflected in the current CRDT state:
                // LWW messages are committed to the state by the provider on creation,
                // the rest (APPEND) is not kept in the state so it must be released straight-away
                using (OutgoingCRDTMessagesSyncBlock outgoingMessagesSyncBlock = GetSerializationSyncBlock())
                    DisposeMessagesNotOwnedByState(outgoingMessagesSyncBlock.Messages);

                // Create CRDT Messages from the current state
                // we know exactly how big the array should be
                int messagesCount = crdtProtocol.GetMessagesCount();
                metrics.MessagesToScene.Add(messagesCount);
                ProcessedCRDTMessage[] processedMessages = sharedPoolsProvider.GetSerializationCrdtMessagesPool(messagesCount);

                int currentStatePayloadLength = crdtProtocol.CreateMessagesFromTheCurrentState(processedMessages);

                // We know exactly how many bytes we need to serialize
                PoolableByteArray serializationBufferPoolable = sharedPoolsProvider.GetSerializedStateBytesPool(currentStatePayloadLength);
                Span<byte> currentStateSpan = serializationBufferPoolable.Span;

                // Serialize the current state

                for (var i = 0; i < messagesCount; i++)
                    crdtSerializer.Serialize(ref currentStateSpan, in processedMessages[i]);

                // Messages are serialized, we no longer need them in the managed form
                sharedPoolsProvider.ReleaseSerializationCrdtMessagesPool(processedMessages);

                // Return the buffer to the caller
                return serializationBufferPoolable;
            }
            catch (Exception e)
            {
                exceptionsHandler.OnEngineException(e, ReportCategory.CRDT);
                return PoolableByteArray.EMPTY;
            }
            finally { Profiler.EndThreadProfiling(); }
        }

        private OutgoingCRDTMessagesSyncBlock GetSerializationSyncBlock() =>
            outgoingCrtdMessagesProvider.GetSerializationSyncBlock(PendingMessageProcessor);

        private PoolableByteArray SerializeOutgoingCRDTMessages()
        {
            try
            {
                outgoingMessagesSampler.Begin();

                PoolableByteArray serializationBufferPoolable;

                using (OutgoingCRDTMessagesSyncBlock outgoingMessagesSyncBlock = GetSerializationSyncBlock())
                {
                    serializationBufferPoolable =
                        sharedPoolsProvider.GetSerializedStateBytesPool(outgoingMessagesSyncBlock.PayloadLength);

                    SerializeOutgoingCRDTMessages(outgoingMessagesSyncBlock.Messages, serializationBufferPoolable.Span);

                    DisposeMessagesNotOwnedByState(outgoingMessagesSyncBlock.Messages);

                    metrics.MessagesToScene.Add(outgoingMessagesSyncBlock.Messages.Count);
                }

                outgoingMessagesSampler.End();
                return serializationBufferPoolable;
            }
            catch (Exception e)
            {
                exceptionsHandler.OnEngineException(e, ReportCategory.CRDT);
                return PoolableByteArray.EMPTY;
            }
        }

        /// <summary>
        ///     LWW messages (PUT/DELETE_COMPONENT) are committed to the local CRDT state when they are created
        ///     (see <see cref="ICRDTProtocol.CreateAndCommitPutMessage" />) so their data is owned by the state.
        ///     The data of the remaining messages (APPEND) is not kept in <see cref="ICRDTProtocol" /> so it must be released immediately
        /// </summary>
        private static void DisposeMessagesNotOwnedByState(IReadOnlyList<ProcessedCRDTMessage> outgoingMessages)
        {
            for (var i = 0; i < outgoingMessages.Count; i++)
            {
                CRDTMessage message = outgoingMessages[i].message;

                if (message.Type is not CRDTMessageType.PUT_COMPONENT and not CRDTMessageType.DELETE_COMPONENT)
                    message.Data.Dispose();
            }
        }

        // Use mutex to apply command buffer from the background thread instead of synchronizing by the main one

        private void ApplySyncCommandBuffer(IWorldSyncCommandBuffer worldSyncBuffer)
        {
            try
            {
                using MultiThreadSync.Scope mutex = multiThreadSync.GetScope(syncOwner);

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

            foreach (ProcessedCRDTMessage processedCRDTMessage in outgoingMessages) { crdtSerializer.Serialize(ref span, in processedCRDTMessage); }
        }
    }
}
