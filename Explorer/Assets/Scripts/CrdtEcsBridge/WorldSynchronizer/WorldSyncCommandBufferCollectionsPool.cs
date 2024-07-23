using CRDT;
using DCL.Optimization.Pools;
using DCL.Optimization.ThreadSafePool;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.Pool;

namespace CrdtEcsBridge.WorldSynchronizer
{
    /// <summary>
    ///     Internal Collections Provider for <see cref="WorldSyncCommandBuffer" />.
    ///     Should be used for a single scene instance
    /// </summary>
    internal class WorldSyncCommandBufferCollectionsPool : IDisposable
    {
        private static readonly ThreadSafeObjectPool<WorldSyncCommandBufferCollectionsPool> POOL = new (
            () => new WorldSyncCommandBufferCollectionsPool(), defaultCapacity: PoolConstants.SCENES_COUNT);

        private readonly IObjectPool<Dictionary<int, BatchState>> innerDictionariesPool = new ObjectPool<Dictionary<int, BatchState>>(

            // just preallocate an array big enough
            createFunc: () => new Dictionary<int, BatchState>(2048),
            actionOnRelease: dictionary => dictionary.Clear(),
            defaultCapacity: 8,
            maxSize: 2048 * 8,

            // hot path
            collectionCheck: false
        );

        private Dictionary<CRDTEntity, Dictionary<int, BatchState>> mainDictionary = new (1024, CRDTEntityComparer.INSTANCE);
        private List<CRDTEntity> deletedEntities = new (256);
        private bool disposed;

        private WorldSyncCommandBufferCollectionsPool() { }

        public void Dispose()
        {
            if (disposed)
                return;

            disposed = true;

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

        public static WorldSyncCommandBufferCollectionsPool Create() =>
            POOL.Get();

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
    }
}
