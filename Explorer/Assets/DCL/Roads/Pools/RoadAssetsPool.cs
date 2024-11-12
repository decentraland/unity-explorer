#nullable enable
using DCL.Optimization.PerformanceBudgeting;
using DCL.Optimization.Pools;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;
using Utility;
using Object = UnityEngine.Object;

namespace DCL.LOD
{
    public class RoadAssetsPool : IRoadAssetPool, IDisposable
    {
        private const string DEFAULT_ROAD_KEY = "OpenRoad_0";

        /// <summary>
        /// The amount of instances of each type of road asset that will be created from the beginning in the pools.
        /// </summary>
        private const int DEFAULT_POOL_CAPACITY = 20;

        /// <summary>
        /// The amount of instances that will be created from the beginning in the pools for "long roads". Long roads are very heavy assets, in terms of memory, whose pools should be pre-warmed with just a few instances, as less as possible.
        /// </summary>
        private const int DEFAULT_LONGROAD_POOL_CAPACITY = 1;

        /// <summary>
        /// When a pool is filled and more instances are required, this is the minimum amount to add to the pool.
        /// </summary>
        private const int POOLS_MIN_NEW_INSTANCES = 1;

        /// <summary>
        /// When a pool is filled and more instances are required, this is the maximum amount to add to the pool.
        /// </summary>
        private const int POOLS_MAX_NEW_INSTANCES = 20;

        private readonly Transform roadAssetParent;

        private readonly Dictionary<string, IObjectPool<Transform>> roadAssetPoolDictionary;

        public RoadAssetsPool(IReadOnlyList<GameObject> roadPrefabs, IComponentPoolsRegistry? componentPoolsRegistry = null)
        {

            var poolRoot = componentPoolsRegistry?.RootContainerTransform();
            roadAssetParent = new GameObject("POOL_CONTAINER_Road_Assets").transform;
            roadAssetParent.parent = poolRoot;

            roadAssetPoolDictionary = new Dictionary<string, IObjectPool<Transform>>();

            foreach (GameObject gameObject in roadPrefabs)
            {
                int poolCapacity = gameObject.CompareTag("LongRoad") ? DEFAULT_LONGROAD_POOL_CAPACITY : DEFAULT_POOL_CAPACITY;

                IObjectPool<Transform> roadAssetPool
                    = new ObjectPool<Transform>(() => Object.Instantiate(gameObject, roadAssetParent).transform,
                        t => t.gameObject.SetActive(true), t => t.gameObject.SetActive(false), actionOnDestroy: UnityObjectUtils.SafeDestroyGameObject,
                        defaultCapacity: poolCapacity);

                roadAssetPoolDictionary.Add(gameObject.name, roadAssetPool);
            }

            // Pool pre-warming
            Prewarm();
        }

        public void Dispose()
        {
            Unload(new NullPerformanceBudget(), int.MaxValue);
            UnityObjectUtils.SafeDestroyGameObject(roadAssetParent);
        }

        /// <summary>
        /// Obtains an instance from a road asset pool.
        /// If the pool is full before calling this method, it will grow instantiating more road assets by an amount that depends on the number of elements in the pool.
        /// </summary>
        /// <param name="key">The name of the pool from which to obtain the instance. It matches the name of the prefab used in the pool.</param>
        /// <param name="roadAsset">The obtained instance.</param>
        /// <returns>True if the pool exists; False otherwise.</returns>
        public bool Get(string key, out Transform roadAsset)
        {
            if (roadAssetPoolDictionary.TryGetValue(key, out IObjectPool<Transform> roadAssetPool))
            {
                if (roadAssetPool.CountInactive == 0)
                {
                    int extraInstances = Mathf.Clamp((roadAssetPool as ObjectPool<Transform>).CountAll / 2, POOLS_MIN_NEW_INSTANCES, POOLS_MAX_NEW_INSTANCES);
                    Transform[] precachedInstances = new Transform[extraInstances];

                    for (int i = 0; i < extraInstances; ++i)
                    {
                        precachedInstances[i] = roadAssetPool.Get();
                    }

                    for (int i = 0; i < extraInstances; ++i)
                    {
                        roadAssetPool.Release(precachedInstances[i]);
                    }
                }

                // Debug: Uncomment this to know the content of the pools
                //string log = "RoadAssetPoolDictionary\nTAKING: " + key + "\n";
                //foreach (KeyValuePair<string,IObjectPool<Transform>> keyValuePair in roadAssetPoolDictionary)
                //{
                //    log += keyValuePair.Key + ": " + (keyValuePair.Value as ObjectPool<Transform>).CountAll + " (" + (keyValuePair.Value as ObjectPool<Transform>).CountActive + ")\n";
                //}
                //Debug.Log(log);

                roadAsset = roadAssetPool.Get();

                return true;
            }

            roadAsset = roadAssetPoolDictionary[DEFAULT_ROAD_KEY].Get();
            return false;
        }

        /// <summary>
        /// Returns an instance to its original pool so it can be reused later. It does not free any memory.
        /// </summary>
        /// <param name="key">The name of the prefab of the road asset, which identifies the pool.</param>
        /// <param name="asset">The instance of the prefab of the road asset to be put back into the pool.</param>
        public void Release(string key, Transform asset)
        {
            if (roadAssetPoolDictionary.TryGetValue(key, out IObjectPool<Transform> roadAssetPool))
                roadAssetPool.Release(asset);
            else
                roadAssetPoolDictionary[DEFAULT_ROAD_KEY].Release(asset);
        }

        // Called by the CacheCleaner
        public void Unload(IPerformanceBudget frameTimeBudgetProvider, int maxUnloadAmount)
        {
            var unloadedAmount = 0;

            if (!frameTimeBudgetProvider.TrySpendBudget()) return;

            foreach (KeyValuePair<string, IObjectPool<Transform>> keyValuePair in roadAssetPoolDictionary)
            {
                unloadedAmount += keyValuePair.Value.CountInactive;
                keyValuePair.Value.Clear();

                // Check if the budget is still available or the max unload amount is reached after each operation
                if (!frameTimeBudgetProvider.TrySpendBudget() || unloadedAmount >= maxUnloadAmount)
                    break;
            }
        }

        /// <summary>
        /// Activates / deactivates all road assets.
        /// </summary>
        /// <param name="isVisible">Whether to activate or not all road assets.</param>
        public void SwitchVisibility(bool isVisible)
        {
            roadAssetParent.gameObject.SetActive(isVisible);
        }

        /// <summary>
        /// Creates an initial number of instances in the pool of each road asset type. There are special road assets tagged with 'LongRoad', which
        /// are much heavy than normal assets and only need a few of instances.
        /// </summary>
        public void Prewarm()
        {
            foreach (var entry in roadAssetPoolDictionary)
            {
                IObjectPool<Transform> roadAssetPool = entry.Value;

                // We can't access the prefab used for instantiating pool elements so we have to get a first instance and get the data from there
                Transform precachedInstance = roadAssetPool.Get();

                int poolCapacity = precachedInstance.gameObject.CompareTag("LongRoad") ? DEFAULT_LONGROAD_POOL_CAPACITY : DEFAULT_POOL_CAPACITY;

                Transform[] precachedInstances = new Transform[poolCapacity];
                precachedInstances[0] = precachedInstance;

                for (int i = 1; i < poolCapacity; ++i) // Note it starts at 1, we already have a first instance
                {
                    precachedInstances[i] = roadAssetPool.Get();
                }

                for (int i = 0; i < poolCapacity; ++i)
                {
                    roadAssetPool.Release(precachedInstances[i]);
                }
            }
        }
    }
}
