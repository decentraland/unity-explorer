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
        internal readonly List<PendingMessage> messages = MESSAGES_SHARED_POOL.Get();

        private readonly ISDKComponentsRegistry componentsRegistry;
        private readonly ICRDTProtocol crdtProtocol;
        private readonly ICRDTMemoryAllocator memoryAllocator;

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
            lock (messages)
            {
                var key = new OutgoingMessageKey(entity, componentBridge.Id);

                if (lwwMessageIndices.TryGetValue(key, out int lwwIndex))
                {
                    // Release previous message to the pool
                    PendingMessage previousMessage = messages[lwwIndex];

                    // for delete messages IMessage is not created
                    if (previousMessage.MessageType == CRDTMessageType.PUT_COMPONENT)
                        previousMessage.Bridge.Pool.Release(previousMessage.Message);

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

            lock (messages) { messages.Add(new PendingMessage(message, componentBridge, entity, CRDTMessageType.APPEND_COMPONENT, timestamp)); }

            return message;
        }

        private bool TryGetComponentBridge<T>(out SDKComponentBridge componentBridge) where T: IMessage
        {
            if (!componentsRegistry.TryGet<T>(out componentBridge))
            {
                ReportHub.LogWarning(new ReportData(ReportCategory.CRDT_ECS_BRIDGE, ReportHint.AssemblyStatic), $"SDK Component {typeof(T)} is not registered");
                return false;
            }

            return true;
        }

        public OutgoingCRDTMessagesSyncBlock GetSerializationSyncBlock()
        {
            // Process all pending messages

            List<ProcessedCRDTMessage> processedMessages = OutgoingCRDTMessagesSyncBlock.MESSAGES_SHARED_POOL.Get();

            // While we do it we must synchronize
            lock (messages)
            {
                for (var i = 0; i < messages.Count; i++)
                {
                    PendingMessage pendingMessage = messages[i];

                    IMemoryOwner<byte> memory;

                    switch (pendingMessage.MessageType)
                    {
                        case CRDTMessageType.PUT_COMPONENT:
                            memory = memoryAllocator.GetMemoryBuffer(pendingMessage.Message.CalculateSize());
                            pendingMessage.Bridge.Serializer.SerializeInto(pendingMessage.Message, memory.Memory.Span);
                            processedMessages.Add(crdtProtocol.CreatePutMessage(pendingMessage.Entity, pendingMessage.Bridge.Id, memory));
                            break;
                        case CRDTMessageType.APPEND_COMPONENT:
                            memory = memoryAllocator.GetMemoryBuffer(pendingMessage.Message.CalculateSize());
                            pendingMessage.Bridge.Serializer.SerializeInto(pendingMessage.Message, memory.Memory.Span);
                            processedMessages.Add(crdtProtocol.CreateAppendMessage(pendingMessage.Entity, pendingMessage.Bridge.Id, pendingMessage.Timestamp, memory));
                            break;
                        case CRDTMessageType.DELETE_COMPONENT:
                            processedMessages.Add(crdtProtocol.CreateDeleteMessage(pendingMessage.Entity, pendingMessage.Bridge.Id));
                            break;
                    }
                }

                messages.Clear();
                lwwMessageIndices.Clear();
            }

            // This list will be released on block.Dispose()
            return new OutgoingCRDTMessagesSyncBlock(processedMessages);
        }

        internal readonly struct PendingMessage
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
