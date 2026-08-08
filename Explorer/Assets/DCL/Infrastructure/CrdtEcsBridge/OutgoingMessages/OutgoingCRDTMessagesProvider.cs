using CRDT;
using CRDT.Memory;
using CRDT.Protocol;
using CRDT.Protocol.Factory;
using CrdtEcsBridge.Components;
using DCL.Diagnostics;
using DCL.Optimization.Pools;
using DCL.Optimization.ThreadSafePool;
using Google.Protobuf;
using System;
using System.Buffers;
using System.Collections.Generic;

namespace CrdtEcsBridge.OutgoingMessages
{
    public class OutgoingCRDTMessagesProvider : IOutgoingCRDTMessagesProvider
    {
        internal static readonly ThreadSafeDictionaryPool<OutgoingMessageKey, int> INDICES_SHARED_POOL =
            new (64, PoolConstants.SCENES_COUNT, new EqualityComparer());

        internal static readonly ThreadSafeListPool<PendingMessage> MESSAGES_SHARED_POOL =
            new (64, PoolConstants.SCENES_COUNT);

        internal readonly Dictionary<OutgoingMessageKey, int> lwwMessageIndices = INDICES_SHARED_POOL.Get();
        internal List<PendingMessage> messages = MESSAGES_SHARED_POOL.Get();

        private readonly ISDKComponentsRegistry componentsRegistry;
        private readonly ICRDTProtocol crdtProtocol;
        private readonly ICRDTMemoryAllocator memoryAllocator;

        /// <summary>
        ///     Guards <see cref="messages" /> and <see cref="lwwMessageIndices" />: a dedicated object is required
        ///     because <see cref="messages" /> is swapped with the spare list on serialization
        /// </summary>
        private readonly object writeLock = new ();

        /// <summary>
        ///     The second buffer <see cref="messages" /> is swapped with so the per-message serialization
        ///     runs outside the lock and does not stall the main-thread systems writing new messages
        /// </summary>
        private List<PendingMessage> spareMessages = MESSAGES_SHARED_POOL.Get();

        public OutgoingCRDTMessagesProvider(ISDKComponentsRegistry componentsRegistry, ICRDTProtocol crdtProtocol, ICRDTMemoryAllocator memoryAllocator)
        {
            this.componentsRegistry = componentsRegistry;
            this.crdtProtocol = crdtProtocol;
            this.memoryAllocator = memoryAllocator;
        }

        public void Dispose()
        {
            INDICES_SHARED_POOL.Release(lwwMessageIndices);
            MESSAGES_SHARED_POOL.Release(messages);
            MESSAGES_SHARED_POOL.Release(spareMessages);
        }

        public void AddDeleteMessage<TMessage>(CRDTEntity entity) where TMessage: class, IMessage
        {
            if (!TryGetComponentBridge<TMessage>(out SDKComponentBridge componentBridge)) return;

            AddLwwMessage(entity, componentBridge, new PendingMessage(null, componentBridge, entity, CRDTMessageType.DELETE_COMPONENT));
        }

        public TMessage AddPutMessage<TMessage, TData>(Action<TMessage, TData> prepareMessage, CRDTEntity entity, TData data) where TMessage: class, IMessage
        {
            if (!TryGetComponentBridge<TMessage>(out SDKComponentBridge componentBridge)) return null;

            // take the component from the pool and prepare it
            // avoid serialization on the site of the caller
            // serialization will be called from EngineAPIImplementation
            var message = (TMessage)componentBridge.Pool.Rent();
            prepareMessage?.Invoke(message, data);
            var newMessage = new PendingMessage(message, componentBridge, entity, CRDTMessageType.PUT_COMPONENT);
            AddLwwMessage(entity, componentBridge, newMessage);
            return message;
        }

        public void AddPutMessage<TMessage>(TMessage message, CRDTEntity entity) where TMessage: class, IMessage
        {
            if (!TryGetComponentBridge<TMessage>(out SDKComponentBridge componentBridge)) return;

            var newMessage = new PendingMessage(message, componentBridge, entity, CRDTMessageType.PUT_COMPONENT);
            AddLwwMessage(entity, componentBridge, newMessage);
        }

        private void AddLwwMessage(CRDTEntity entity, SDKComponentBridge componentBridge, in PendingMessage newMessage)
        {
            lock (writeLock)
            {
                var key = new OutgoingMessageKey(entity, componentBridge.Id);

                if (lwwMessageIndices.TryGetValue(key, out int lwwIndex))
                {
                    // Release previous message to the pool
                    PendingMessage previousMessage = messages[lwwIndex];

                    // for delete messages IMessage is not created
                    if (previousMessage.MessageType == CRDTMessageType.PUT_COMPONENT)
                    {
                        previousMessage.Bridge.Pool.Release(previousMessage.Message);
                    }

                    messages[lwwIndex] = newMessage;
                }
                else
                {
                    lwwMessageIndices[key] = messages.Count;
                    messages.Add(newMessage);
                }
            }
        }

        public TMessage AppendMessage<TMessage, TData>(Action<TMessage, TData> prepareMessage, CRDTEntity entity, int timestamp, TData data) where TMessage: class, IMessage
        {
            if (!TryGetComponentBridge<TMessage>(out SDKComponentBridge componentBridge)) return null;

            var message = (TMessage)componentBridge.Pool.Rent();
            prepareMessage(message, data);

            lock (writeLock) { messages.Add(new PendingMessage(message, componentBridge, entity, CRDTMessageType.APPEND_COMPONENT, timestamp)); }

            return message;
        }

        private bool TryGetComponentBridge<T>(out SDKComponentBridge componentBridge) where T: IMessage
        {
            if (!componentsRegistry.TryGet<T>(out componentBridge))
            {
                ReportHub.LogWarning(new ReportData(ReportCategory.CRDT_ECS_BRIDGE, ReportDebounce.AssemblyStatic), $"SDK Component {typeof(T)} is not registered");
                return false;
            }

            return true;
        }

        public OutgoingCRDTMessagesSyncBlock GetSerializationSyncBlock(Action<PendingMessage> actOnPendingMessage)
        {
            // Process all pending messages

            List<ProcessedCRDTMessage> processedMessages = OutgoingCRDTMessagesSyncBlock.MESSAGES_SHARED_POOL.Get();

            List<PendingMessage> toSerialize;

            // Detach the pending batch under the lock: producers keep writing into the spare list
            // while serialization (the expensive part) runs lock-free below
            lock (writeLock)
            {
                toSerialize = messages;
                messages = spareMessages;
                spareMessages = toSerialize;
                lwwMessageIndices.Clear();
            }

            // Safe without the lock: this method is invoked from the scene runtime thread only (single consumer)
            // so nothing else touches the detached list
            for (var i = 0; i < toSerialize.Count; i++)
            {
                PendingMessage pendingMessage = toSerialize[i];

                actOnPendingMessage?.Invoke(pendingMessage);

                IMemoryOwner<byte> memory;

                switch (pendingMessage.MessageType)
                {
                    case CRDTMessageType.PUT_COMPONENT:
                        memory = memoryAllocator.GetMemoryBuffer(pendingMessage.Message.CalculateSize());
                        pendingMessage.Bridge.Serializer.SerializeInto(pendingMessage.Message, memory.Memory.Span);
                        pendingMessage.Bridge.Pool.Release(pendingMessage.Message);
                        processedMessages.Add(crdtProtocol.CreatePutMessage(pendingMessage.Entity, pendingMessage.Bridge.Id, memory));
                        break;
                    case CRDTMessageType.APPEND_COMPONENT:
                        memory = memoryAllocator.GetMemoryBuffer(pendingMessage.Message.CalculateSize());
                        pendingMessage.Bridge.Serializer.SerializeInto(pendingMessage.Message, memory.Memory.Span);
                        pendingMessage.Bridge.Pool.Release(pendingMessage.Message);
                        processedMessages.Add(crdtProtocol.CreateAppendMessage(pendingMessage.Entity, pendingMessage.Bridge.Id, pendingMessage.Timestamp, memory));
                        break;
                    case CRDTMessageType.DELETE_COMPONENT:
                        processedMessages.Add(crdtProtocol.CreateDeleteMessage(pendingMessage.Entity, pendingMessage.Bridge.Id));
                        break;
                }
            }

            toSerialize.Clear();

            // This list will be released on block.Dispose()
            return new OutgoingCRDTMessagesSyncBlock(processedMessages);
        }

        public readonly struct PendingMessage
        {
            public readonly IMessage Message;
            public readonly CRDTEntity Entity;
            public readonly SDKComponentBridge Bridge;
            public readonly CRDTMessageType MessageType;

            /// <summary>
            ///     Needed for append messages
            /// </summary>
            public readonly int Timestamp;

            public PendingMessage(IMessage message, SDKComponentBridge bridge, CRDTEntity entity, CRDTMessageType messageType, int timestamp = 0)
            {
                Message = message;
                Bridge = bridge;
                Entity = entity;
                MessageType = messageType;
                Timestamp = timestamp;
            }
        }

        private class EqualityComparer : IEqualityComparer<OutgoingMessageKey>
        {
            public bool Equals(OutgoingMessageKey x, OutgoingMessageKey y) =>
                x.Entity.Equals(y.Entity) && x.ComponentId == y.ComponentId;

            public int GetHashCode(OutgoingMessageKey obj) =>
                HashCode.Combine(obj.Entity.Id, obj.ComponentId);
        }
    }
}
