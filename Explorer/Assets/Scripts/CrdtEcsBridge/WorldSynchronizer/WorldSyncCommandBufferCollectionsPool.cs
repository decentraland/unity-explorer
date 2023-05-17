using CRDT;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.Pool;
using Utility.ThreadSafePool;

namespace CrdtEcsBridge.WorldSynchronizer
{
    /// <summary>
    ///     Internal Collections Provider for <see cref="WorldSyncCommandBuffer" />.
    ///     Should be used for a single scene instance
    /// </summary>
    internal class WorldSyncCommandBufferCollectionsPool : IDisposable
    {
        /// <summary>
        ///     Denotes the number of entities that can be stored without allocations after warming up
        /// </summary>
        internal const int INNER_DICTIONARIES_POOL_CAPACITY = 1024;

        /// <summary>
        ///     Denotes the initial capacity of components that can be stored per entity
        /// </summary>
        internal const int INNER_DICTIONARY_INITIAL_CAPACITY = 256;

        /// <summary>
        ///     Denotes the maximum number of components per entity that can be stored without allocations after warming up
        /// </summary>
        internal const int INNER_DICTIONARY_MAX_CAPACITY = INNER_DICTIONARY_INITIAL_CAPACITY * INNER_DICTIONARIES_POOL_CAPACITY;

        private static readonly ThreadSafeObjectPool<WorldSyncCommandBufferCollectionsPool> POOL = new (
            () => new WorldSyncCommandBufferCollectionsPool());

        private static readonly ThreadSafeObjectPool<BatchState> BATCH_STATE_POOL = new (
            () => new BatchState(),
            actionOnRelease: state => state.deserializationTarget = null,
            defaultCapacity: INNER_DICTIONARY_INITIAL_CAPACITY,
            maxSize: INNER_DICTIONARY_MAX_CAPACITY,

            // Omit checking collections, it is a hot path on the main thread
            collectionCheck: false);

        private Dictionary<CRDTEntity, Dictionary<int, BatchState>> mainDictionary = new (INNER_DICTIONARIES_POOL_CAPACITY, CRDTEntityComparer.INSTANCE);
        private List<CRDTEntity> deletedEntities = new (256);

        private readonly IObjectPool<Dictionary<int, BatchState>> innerDictionariesPool = new ObjectPool<Dictionary<int, BatchState>>(

            // just preallocate an array big enough
            createFunc: () => new Dictionary<int, BatchState>(INNER_DICTIONARY_INITIAL_CAPACITY),
            actionOnRelease: dictionary => dictionary.Clear(),
            defaultCapacity: INNER_DICTIONARY_INITIAL_CAPACITY,
            maxSize: INNER_DICTIONARIES_POOL_CAPACITY
        );

        public static WorldSyncCommandBufferCollectionsPool Create() =>
            POOL.Get();

        private WorldSyncCommandBufferCollectionsPool() { }

        public Dictionary<CRDTEntity, Dictionary<int, BatchState>> GetMainDictionary()
        {
            if (mainDictionary == null)
                throw new ThreadStateException($"{nameof(mainDictionary)} was already rented but not released");

            Dictionary<CRDTEntity, Dictionary<int, BatchState>> dict = mainDictionary;
            mainDictionary = null;
            return dict;
        }

        public void ReleaseMainDictionary(Dictionary<CRDTEntity, Dictionary<int, BatchState>> mainDictionary)
        {
            this.mainDictionary = mainDictionary;
            mainDictionary.Clear();
        }

        public List<CRDTEntity> GetDeletedEntities()
        {
            if (deletedEntities == null)
                throw new ThreadStateException($"{nameof(deletedEntities)} was already rented but not released");

            List<CRDTEntity> list = deletedEntities;
            deletedEntities = null;
            return list;
        }

        public void ReleaseDeletedEntities(List<CRDTEntity> deletedEntities)
        {
            this.deletedEntities = deletedEntities;
            this.deletedEntities.Clear();
        }

        public Dictionary<int, BatchState> GetInnerDictionary() =>
            innerDictionariesPool.Get();

        public void ReleaseInnerDictionary(Dictionary<int, BatchState> inner)
        {
            innerDictionariesPool.Release(inner);
        }

        public BatchState GetBatchState() =>
            BATCH_STATE_POOL.Get();

        public void ReleaseBatchState(BatchState batchState) =>
            BATCH_STATE_POOL.Release(batchState);

        public void Dispose()
        {
            if (mainDictionary == null)
            {
                Debug.LogError($"{nameof(WorldSyncCommandBufferCollectionsPool)} can't be disposed: {nameof(mainDictionary)} leaked");
                return;
            }

            if (deletedEntities == null)
            {
                Debug.LogError($"{nameof(WorldSyncCommandBufferCollectionsPool)} can't be disposed: {nameof(deletedEntities)} leaked");
                return;
            }

            POOL.Release(this);
        }
    }
}
