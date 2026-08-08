using Collections.Pooled;
using CRDT.Memory;
using CRDT.Protocol.Factory;
using CRDT.Serializer;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace CRDT.Protocol
{
    public class CRDTProtocol : ICRDTProtocol, IComparer<CRDTProtocol.EntityComponentData>
    {
        private const int MAX_APPEND_COMPONENTS_COUNT = 100;

        /// <summary>
        ///     Roughly the number of distinct SDK component types a scene touches: presizing avoids rehash cascades
        ///     during scene-load PUT storms (every rehash re-rents and copies the pooled backing arrays)
        /// </summary>
        private const int LWW_COMPONENTS_CAPACITY = 64;

        /// <summary>
        ///     Entities per component bucket; hot buckets (e.g. Transform) will still grow beyond it
        /// </summary>
        private const int LWW_ENTITIES_CAPACITY = 128;

        private const int APPEND_COMPONENTS_CAPACITY = 8;
        private const int APPEND_ENTITIES_CAPACITY = 32;
        private const int DELETED_ENTITIES_CAPACITY = 256;

        private State crdtState;

        internal ref State CRDTState => ref crdtState;

        public CRDTProtocol()
        {
            crdtState = new State(
                new PooledDictionary<int, int>(DELETED_ENTITIES_CAPACITY),
                new PooledDictionary<int, PooledDictionary<CRDTEntity, EntityComponentData>>(LWW_COMPONENTS_CAPACITY),
                new PooledDictionary<int, PooledDictionary<CRDTEntity, PooledList<EntityComponentData>>>(APPEND_COMPONENTS_CAPACITY));
        }

        public void Dispose()
        {
            // Disposing every outer and inner collection will return their internals to the pool
            // Pools themselves are thread-safe according to https://learn.microsoft.com/en-us/dotnet/api/system.buffers.arraypool-1?view=netstandard-2.1

            foreach (PooledDictionary<CRDTEntity, PooledList<EntityComponentData>> outer in crdtState.appendComponents.Values)
            {
                foreach (PooledList<EntityComponentData> inner in outer.Values)
                {
                    foreach (EntityComponentData entityComponentData in inner)
                        entityComponentData.Data.Dispose();

                    inner.Dispose();
                }

                outer.Dispose();
            }

            crdtState.appendComponents.Dispose();

            foreach (PooledDictionary<CRDTEntity, EntityComponentData> outer in crdtState.lwwComponents.Values)
            {
                foreach (EntityComponentData inner in outer.Values)
                    inner.Data.Dispose();

                outer.Dispose();
            }

            crdtState.lwwComponents.Dispose();
            crdtState.messagesCount = 0;
        }

        public int GetMessagesCount() =>
            crdtState.messagesCount;

        public CRDTReconciliationResult ProcessMessage(in CRDTMessage message)
        {
            CRDTEntity entityId = message.EntityId;
            int entityNumber = entityId.EntityNumber;
            int entityVersion = entityId.EntityVersion;

            // Instead of storing by "entityId", store by "entityNumber" as SDK will reuse them and the set will be smaller
            bool entityNumberWasDeleted = crdtState.deletedEntities.TryGetValue(entityNumber, out int deletedVersion);

            if (entityNumberWasDeleted && deletedVersion >= entityVersion)

                // Entity was already deleted so no actions are required
                return new CRDTReconciliationResult(CRDTStateReconciliationResult.EntityWasDeleted, CRDTReconciliationEffect.NoChanges);

            switch (message.Type)
            {
                case CRDTMessageType.DELETE_ENTITY:
                    DeleteEntity(entityId, entityNumber, entityVersion, entityNumberWasDeleted);
                    return new CRDTReconciliationResult(CRDTStateReconciliationResult.EntityDeleted, CRDTReconciliationEffect.EntityDeleted);
                case CRDTMessageType.APPEND_COMPONENT:
                    // The results of his branch is ignored as there are probably no SDK components with "APPEND" type
                    // but the state should be stored as these components are produced by the client
                    // and in theory they still must be reconciled
                    return TryAppendComponent(in message)
                        ? new CRDTReconciliationResult(CRDTStateReconciliationResult.StateAppendedData, CRDTReconciliationEffect.ComponentAdded)
                        : new CRDTReconciliationResult(CRDTStateReconciliationResult.NoChanges, CRDTReconciliationEffect.NoChanges);

                // Effectively it is the same logic that updates the LWW set, the only difference is in Data presence
                // For DELETE_COMPONENT it is "Empty"
                case CRDTMessageType.PUT_COMPONENT:
                case CRDTMessageType.DELETE_COMPONENT:
                    GetReconciliationResultFromLWWMessage(in message, out CRDTReconciliationEffect overrideEffect, out CRDTReconciliationEffect newComponentEffect);
                    return UpdateLWWState(in message, overrideEffect, newComponentEffect);

                // Server authoritative message - forces component state regardless of timestamp
                case CRDTMessageType.AUTHORITATIVE_PUT_COMPONENT:
                    GetReconciliationResultFromLWWMessage(in message, out CRDTReconciliationEffect authOverrideEffect, out CRDTReconciliationEffect authNewComponentEffect);
                    CRDTReconciliationResult result = UpdateLWWState(in message, authOverrideEffect, authNewComponentEffect, ignoreTimestamp: true);
                    return result;
            }

            throw new NotSupportedException($"Message type {message.Type} is not supported");
        }

        public int CreateMessagesFromTheCurrentState(ProcessedCRDTMessage[] preallocatedArray) =>
            crdtState.CreateMessagesFromTheCurrentState(preallocatedArray);

        public IReadOnlyList<(int componentId, EntityComponentData)> GetStateForEntityDebug(CRDTEntity entity)
        {
            var componentList = new List<(int componentId, EntityComponentData)>();

            foreach ((int componentId, PooledDictionary<CRDTEntity, EntityComponentData> value) in crdtState.lwwComponents)
            {
                if (value.TryGetValue(entity, out EntityComponentData componentData))
                    componentList.Add((componentId, componentData));
            }

            return componentList;
        }

        public ProcessedCRDTMessage CreateAppendMessage(CRDTEntity entity, int componentId, int timestamp, in IMemoryOwner<byte> data) =>
            CRDTMessagesFactory.CreateAppendMessage(entity, componentId, timestamp, data);

        public ProcessedCRDTMessage CreateAndCommitPutMessage(CRDTEntity entity, int componentId, in IMemoryOwner<byte> data) =>
            CreateAndCommitLwwMessage(CRDTMessageType.PUT_COMPONENT, entity, componentId, data);

        public ProcessedCRDTMessage CreateAndCommitDeleteMessage(CRDTEntity entity, int componentId) =>
            CreateAndCommitLwwMessage(CRDTMessageType.DELETE_COMPONENT, entity, componentId, EmptyMemoryOwner<byte>.EMPTY);

        /// <summary>
        ///     Creates the LWW message and writes it into the local CRDT state in a single probe of the state dictionaries.
        ///     The local message is by construction the newest state (its timestamp is the stored one + 1) so the full
        ///     reconciliation performed by <see cref="ProcessMessage" /> would be redundant.
        ///     The CRDT state takes the ownership of <paramref name="data" />.
        /// </summary>
        private ProcessedCRDTMessage CreateAndCommitLwwMessage(CRDTMessageType messageType, CRDTEntity entity, int componentId, in IMemoryOwner<byte> data)
        {
            if (!crdtState.lwwComponents.TryGetValue(componentId, out PooledDictionary<CRDTEntity, EntityComponentData> inner))
                crdtState.lwwComponents[componentId] = inner = new PooledDictionary<CRDTEntity, EntityComponentData>(LWW_ENTITIES_CAPACITY, CRDTEntityComparer.INSTANCE);

            var timestamp = 0;

            if (inner.TryGetValue(entity, out EntityComponentData storedData))
            {
                timestamp = storedData.Timestamp + 1;
                storedData.Data.Dispose();
            }
            else
                crdtState.messagesCount++;

            inner[entity] = new EntityComponentData(timestamp, data, messageType);

            return new ProcessedCRDTMessage(
                new CRDTMessage(messageType, entity, componentId, timestamp, data),
                CRDTMessageSerializationUtils.GetMessageDataLength(messageType, in data));
        }

        private static void GetReconciliationResultFromLWWMessage(in CRDTMessage message, out CRDTReconciliationEffect overrideEffect, out CRDTReconciliationEffect newComponentEffect)
        {
            switch (message.Type)
            {
                case CRDTMessageType.PUT_COMPONENT:
                // Same as PUT_COMPONENT but with authoritative behavior
                case CRDTMessageType.AUTHORITATIVE_PUT_COMPONENT:
                    overrideEffect = CRDTReconciliationEffect.ComponentModified;
                    newComponentEffect = CRDTReconciliationEffect.ComponentAdded;
                    break;
                case CRDTMessageType.DELETE_COMPONENT:
                    overrideEffect = CRDTReconciliationEffect.ComponentDeleted;
                    newComponentEffect = CRDTReconciliationEffect.NoChanges;
                    break;
                default:
                    throw new ArgumentException($"Message type {message.Type} is not LWW");
            }
        }

        private CRDTReconciliationResult UpdateLWWState(in CRDTMessage message, CRDTReconciliationEffect overrideEffect, CRDTReconciliationEffect newComponentEffect, bool ignoreTimestamp = false)
        {
            bool innerSetExists = crdtState.TryGetLWWComponentState(in message, out PooledDictionary<CRDTEntity, EntityComponentData> inner,
                out bool componentExists, out EntityComponentData storedData);

            bool componentWasDeleted = componentExists && storedData.isDeleted;

            // The received message is > than our current value, update our state
            if (!componentExists || storedData.Timestamp < message.Timestamp || ignoreTimestamp)
            {
                UpdateLWWState(innerSetExists, componentExists, inner, in message, ref storedData);

                CRDTReconciliationEffect componentEffect = componentExists && !componentWasDeleted
                    ? overrideEffect
                    : newComponentEffect;

                return new CRDTReconciliationResult(CRDTStateReconciliationResult.StateUpdatedTimestamp, componentEffect);
            }

            // Outdated Message. The client state will be resent
            // Nothing to change in the client CRDT state
            if (storedData.Timestamp > message.Timestamp)
                return new CRDTReconciliationResult(CRDTStateReconciliationResult.StateOutdatedTimestamp, CRDTReconciliationEffect.NoChanges);

            int compareDataResult = CRDTMessageComparer.CompareData(in storedData.Data, message.Data);

            switch (compareDataResult)
            {
                case 0:
                    // Right the same message, dispose the data
                    message.Data.Dispose();
                    return new CRDTReconciliationResult(CRDTStateReconciliationResult.NoChanges, CRDTReconciliationEffect.NoChanges);
                case > 0:
                    // The stored message is newer, dispose the data
                    message.Data.Dispose();
                    return new CRDTReconciliationResult(CRDTStateReconciliationResult.StateOutdatedData, CRDTReconciliationEffect.NoChanges);
                default:
                    UpdateLWWState(true, true, inner, in message, ref storedData);

                    // The local state is updated
                    return new CRDTReconciliationResult(CRDTStateReconciliationResult.StateUpdatedData, componentWasDeleted ? newComponentEffect : overrideEffect);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void UpdateLWWState(bool innerSetExists, bool componentExists, PooledDictionary<CRDTEntity, EntityComponentData> inner,
            in CRDTMessage crdtMessage, ref EntityComponentData componentData)
        {
            if (!innerSetExists)
                crdtState.lwwComponents[crdtMessage.ComponentId] = inner = new PooledDictionary<CRDTEntity, EntityComponentData>(LWW_ENTITIES_CAPACITY, CRDTEntityComparer.INSTANCE);

            if (!componentExists)
                crdtState.messagesCount++;
            else
                componentData.Data.Dispose();

            UpdateLWWState(in crdtMessage, ref componentData);

            // We don't have a ref-wise API for Dictionary Values so we have to assign the data back
            inner[crdtMessage.EntityId] = componentData;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void UpdateLWWState(in CRDTMessage crdtMessage, ref EntityComponentData componentData)
        {
            componentData.Timestamp = crdtMessage.Timestamp;
            componentData.Data = crdtMessage.Data;
            componentData.Type = crdtMessage.Type;
        }

        private void DeleteEntity(CRDTEntity entityId, int entityNumber, int entityVersion, bool entityWasDeleted)
        {
            crdtState.deletedEntities[entityNumber] = entityVersion;

            if (!entityWasDeleted)
                crdtState.messagesCount++;

            foreach (PooledDictionary<CRDTEntity, EntityComponentData> componentsStorage in crdtState.lwwComponents.Values)
            {
                // Remove with the out value probes the bucket once, unlike the TryGetValue + Remove pair
                if (componentsStorage.Remove(entityId, out EntityComponentData componentData))
                {
                    componentData.Data.Dispose();
                    crdtState.messagesCount--;
                }
            }

            foreach (KeyValuePair<int, PooledDictionary<CRDTEntity, PooledList<EntityComponentData>>> componentsSet in crdtState.appendComponents)
            {
                if (componentsSet.Value.Remove(entityId, out PooledList<EntityComponentData> list))
                {
                    crdtState.messagesCount -= list.Count;

                    foreach (EntityComponentData entityComponentData in list)
                        entityComponentData.Data.Dispose();

                    list.Dispose();
                }
            }
        }

        /// <summary>
        ///     The execution is expensive, consider invoking from a background thread only
        /// </summary>
        private bool TryAppendComponent(in CRDTMessage message)
        {
            var newData = new EntityComponentData(message.Timestamp, message.Data, message.Type);
            bool outerCollectionExists;

            if ((outerCollectionExists = crdtState.appendComponents.TryGetValue(message.ComponentId, out PooledDictionary<CRDTEntity, PooledList<EntityComponentData>> outer))
                && outer.TryGetValue(message.EntityId, out PooledList<EntityComponentData> existingSet))
            {
                int foundIndex = existingSet.BinarySearch(newData, this);

                if (foundIndex < 0)
                {
                    // it should never be "greater", always equal
                    if (existingSet.Count >= MAX_APPEND_COMPONENTS_COUNT)
                    {
                        // instead of removing range, just clean -> it is much cheaper
                        foreach (EntityComponentData entityComponentData in existingSet)
                            entityComponentData.Data.Dispose();

                        existingSet.Clear();
                    }

                    existingSet.Add(newData);
                    crdtState.messagesCount++;
                    return true;
                }

                // If the element (meaning Timestamp + Data) already exists don't do anything
                // It is a weird case, still processed
                return false;
            }

            // In the previous branch we return from the method
            // So to the moment existingSet is always null
            existingSet = new PooledList<EntityComponentData>();
            existingSet.Add(newData);
            crdtState.messagesCount++;

            // No data for the given component exists yet
            if (!outerCollectionExists)

                // Pooled collection will take care of pooling an internal state, there will be a small garbage related to the class itself
                // We can tolerate it
                crdtState.appendComponents[message.ComponentId] = outer = new PooledDictionary<CRDTEntity, PooledList<EntityComponentData>>(APPEND_ENTITIES_CAPACITY, CRDTEntityComparer.INSTANCE);

            outer[message.EntityId] = existingSet;
            return true;
        }

        int IComparer<EntityComponentData>.Compare(EntityComponentData x, EntityComponentData y)
        {
            int diff = x.Timestamp.CompareTo(y.Timestamp);

            // For Binary Search we just need any stable sorting
            return diff == 0 ? CRDTMessageComparer.CompareData(x.Data, y.Data) : diff;
        }

        public struct EntityComponentData
        {
            internal int Timestamp;
            internal IMemoryOwner<byte> Data;
            internal CRDTMessageType Type;

            internal EntityComponentData(int timestamp, IMemoryOwner<byte> data, CRDTMessageType type)
            {
                Timestamp = timestamp;
                Data = data;
                Type = type;
            }

            /// <summary>
            /// Data can be empty but component was "PUT" if its data is default
            /// </summary>
            internal bool isDeleted => Type == CRDTMessageType.DELETE_COMPONENT;
        }

        /// <summary>
        ///     An internal state of the CRDT structures.
        ///     Does not update itself
        /// </summary>
        internal struct State
        {
            /// <summary>
            ///     Entity Number to Entity Version
            /// </summary>
            internal readonly PooledDictionary<int, int> deletedEntities;

            /// <summary>
            ///     Outer key is Component Id
            ///     Inner key is Entity id
            ///     LWW components contain the most recent representation of the operations
            /// </summary>
            /// s
            internal readonly PooledDictionary<int, PooledDictionary<CRDTEntity, EntityComponentData>> lwwComponents;

            /// <summary>
            ///     Outer key is Component Id
            ///     Inner key is Entity id
            ///     In order to comply with "APPEND" concept (all components must be processed) we need to maintain a list
            ///     unlike "PUT" and "DELETE"
            /// </summary>
            /// s
            internal readonly PooledDictionary<int, PooledDictionary<CRDTEntity, PooledList<EntityComponentData>>> appendComponents;

            /// <summary>
            ///     Number of the messages that should be created to represent the current CRDT State
            /// </summary>
            internal int messagesCount;

            public State(PooledDictionary<int, int> deletedEntities, PooledDictionary<int, PooledDictionary<CRDTEntity, EntityComponentData>> lwwComponents, PooledDictionary<int, PooledDictionary<CRDTEntity, PooledList<EntityComponentData>>> appendComponents)
            {
                this.deletedEntities = deletedEntities;
                this.lwwComponents = lwwComponents;
                this.appendComponents = appendComponents;
                messagesCount = 0;
            }

            internal readonly bool TryGetLWWComponentState(in CRDTMessage message, out PooledDictionary<CRDTEntity, EntityComponentData> inner, out bool componentExists,
                out EntityComponentData storedData) =>
                TryGetLWWComponentState(message.EntityId, message.ComponentId, out inner, out componentExists, out storedData);

            internal readonly bool TryGetLWWComponentState(CRDTEntity entity, int componentId, out PooledDictionary<CRDTEntity, EntityComponentData> inner, out bool componentExists,
                out EntityComponentData storedData)
            {
                bool innerSetExists = lwwComponents.TryGetValue(componentId, out inner);
                storedData = default(EntityComponentData);
                componentExists = innerSetExists && inner.TryGetValue(entity, out storedData);
                return innerSetExists;
            }
        }
    }
}
