using System;
using System.Collections.Generic;
using DCL.Optimization.PerformanceBudgeting;
using DCL.Optimization.Pools;
using Nethereum.ABI.Util;
using System.Reflection;
using Unity.VisualScripting.YamlDotNet.Core.Tokens;
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
        /// When a pool is filled and more instances are required, this is the minimum amount to add to the pool.
        /// </summary>
        private const int POOLS_MIN_NEW_INSTANCES = 20;

        /// <summary>
        /// When a pool is filled and more instances are required, this is the maximum amount to add to the pool.
        /// </summary>
        private const int POOLS_MAX_NEW_INSTANCES = 20;

        private readonly Transform roadAssetParent;

        private readonly Dictionary<string, IObjectPool<Transform>> roadAssetPoolDictionary;

        public RoadAssetsPool(IReadOnlyList<GameObject> roadPrefabs)
        {
            roadAssetParent = new GameObject("ROAD_ASSET_POOL").transform;
            roadAssetPoolDictionary = new Dictionary<string, IObjectPool<Transform>>();


            foreach (GameObject gameObject in roadPrefabs)
            {
                IObjectPool<Transform> roadAssetPool
                    = new ObjectPool<Transform>(() => Object.Instantiate(gameObject, roadAssetParent).transform,
                        t => t.gameObject.SetActive(true), t => t.gameObject.SetActive(false), actionOnDestroy: UnityObjectUtils.SafeDestroyGameObject,
                        defaultCapacity: DEFAULT_POOL_CAPACITY);

                // Long roads are very heavy assets, in terms of memory, whose pools should not pre-warmed
                if (!gameObject.CompareTag("LongRoad"))
                {
                    // Pool pre-warming
                    Transform[] precachedInstances = new Transform[DEFAULT_POOL_CAPACITY];

                    for (int i = 0; i < DEFAULT_POOL_CAPACITY; ++i)
                    {
                        precachedInstances[i] = roadAssetPool.Get();
                    }

                    for (int i = 0; i < DEFAULT_POOL_CAPACITY; ++i)
                    {
                        roadAssetPool.Release(precachedInstances[i]);
                    }
                }

                roadAssetPoolDictionary.Add(gameObject.name, roadAssetPool);
            }
        }

        public void Dispose()
        {
            foreach (KeyValuePair<string, IObjectPool<Transform>> keyValuePair in roadAssetPoolDictionary)
                keyValuePair.Value.Clear();

            UnityObjectUtils.SafeDestroy(roadAssetParent);
        }

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

        public void Release(string key, Transform asset)
        {
            if (roadAssetPoolDictionary.TryGetValue(key, out IObjectPool<Transform> roadAssetPool))
                roadAssetPool.Release(asset);
            else
                roadAssetPoolDictionary[DEFAULT_ROAD_KEY].Release(asset);
        }

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

        public void SwitchVisibility(bool isVisible)
        {
            roadAssetParent.gameObject.SetActive(isVisible);
        }
    }
}
