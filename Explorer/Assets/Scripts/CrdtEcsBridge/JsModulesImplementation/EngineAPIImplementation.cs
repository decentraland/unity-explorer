using CRDT.Deserializer;
using CRDT.Protocol;
using CRDT.Protocol.Factory;
using CRDT.Serializer;
using CrdtEcsBridge.OutgoingMessages;
using CrdtEcsBridge.PoolsProviders;
using CrdtEcsBridge.UpdateGate;
using CrdtEcsBridge.WorldSynchronizer;
using DCL.Diagnostics;
using SceneRunner.Scene.ExceptionsHandling;
using SceneRuntime.Apis.Modules.EngineApi;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine.Profiling;
using Utility.Multithreading;

namespace CrdtEcsBridge.JsModulesImplementation
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
        private readonly ISystemGroupsUpdateGate systemGroupsUpdateGate;
        private readonly CustomSampler worldSyncBufferSampler;
        private bool isDisposing;

        private readonly Action<OutgoingCRDTMessagesProvider.PendingMessage> processPendingMessage;

        protected readonly ISharedPoolsProvider sharedPoolsProvider;

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

            processPendingMessage = ProcessPendingMessage;
        }

        public PoolableByteArray CrdtSendToRenderer(ReadOnlyMemory<byte> dataMemory, bool returnData = true)
        {
            // TODO it's dirty, think how to do it better
            if (isDisposing) return PoolableByteArray.EMPTY;

            // Called on the thread where the Scene Runtime is running (background thread)

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

            ApplySyncCommandBuffer(worldSyncBuffer);
            instancePoolsProvider.ReleaseDeserializationMessagesPool(messages);

            return returnData ? SerializeOutgoingCRDTMessages() : PoolableByteArray.EMPTY;
        }

        public PoolableByteArray CrdtGetState()
        {
            if (isDisposing) return PoolableByteArray.EMPTY;

            Profiler.BeginThreadProfiling("SceneRuntime", "CrtdGetState");

            // Invoked on the background thread
            // this method is called rarely but the memory impact is significant

            try
            {
                // Apply outgoing messages straight-away so they are reflected in the current CRDT state
                using (OutgoingCRDTMessagesSyncBlock outgoingMessagesSyncBlock = GetSerializationSyncBlock())
                    SyncOutgoingCRDTMessages(outgoingMessagesSyncBlock.Messages);

                // Create CRDT Messages from the current state
                // we know exactly how big the array should be
                int messagesCount = crdtProtocol.GetMessagesCount();
                ProcessedCRDTMessage[] processedMessages = sharedPoolsProvider.GetSerializationCrdtMessagesPool(messagesCount);

                int currentStatePayloadLength = crdtProtocol.CreateMessagesFromTheCurrentState(processedMessages);

                // We know exactly how many bytes we need to serialize
                var serializationBufferPoolable = sharedPoolsProvider.GetSerializedStateBytesPool(currentStatePayloadLength);
                var currentStateSpan = serializationBufferPoolable.Span;

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

        public virtual void SetIsDisposing()
        {
            isDisposing = true;
        }

        private OutgoingCRDTMessagesSyncBlock GetSerializationSyncBlock() => outgoingCrtdMessagesProvider.GetSerializationSyncBlock(processPendingMessage);

        protected virtual void ProcessPendingMessage(OutgoingCRDTMessagesProvider.PendingMessage pendingMessage)
        {
        }

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

                    SyncOutgoingCRDTMessages(outgoingMessagesSyncBlock.Messages);
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
        ///     If local messages are not written in the local CRDT, the final state
        ///     including timestamp is incorrect. Subsequent creation of propagated messages will result in a wrong timestamp
        /// </summary>
        private void SyncOutgoingCRDTMessages(IReadOnlyList<ProcessedCRDTMessage> outgoingMessages)
        {
            for (var i = 0; i < outgoingMessages.Count; i++)
            {
                SyncCRDTMessage(outgoingMessages[i]);
            }
        }

        private void SyncCRDTMessage(ProcessedCRDTMessage message)
        {
            // We are interested in LWW messages only,
            switch (message.message.Type)
            {
                case CRDTMessageType.DELETE_COMPONENT:
                case CRDTMessageType.PUT_COMPONENT:
                    // instead of processing via CRDTProtocol.ProcessMessage
                    // we can skip part of the logic as we guarantee that the local message is the final valid state (see OutgoingCRDTMessagesProvider.AddLwwMessage)
                    crdtProtocol.EnforceLWWState(message.message);
                    break;
                default:
                    // as this data is not kept in CRDTProtocol it must be released immediately
                    message.message.Data.Dispose();
                    break;
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
            {
                crdtSerializer.Serialize(ref span, in processedCRDTMessage);
            }
        }
    }
}
