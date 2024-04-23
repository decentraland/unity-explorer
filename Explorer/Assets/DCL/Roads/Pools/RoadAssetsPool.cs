﻿using System;
using System.Collections.Generic;
using DCL.Optimization.PerformanceBudgeting;
using DCL.Optimization.Pools;
using UnityEngine;
using UnityEngine.Pool;
using Utility;
using Object = UnityEngine.Object;

namespace DCL.LOD
{
    public class RoadAssetsPool : IRoadAssetPool, IDisposable
    {
        private const string DEFAULT_ROAD_KEY = "OpenRoad_0";
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
                        t => t.gameObject.SetActive(true), t => t.gameObject.SetActive(false), actionOnDestroy: UnityObjectUtils.SafeDestroyGameObject);

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
