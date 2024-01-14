using System;
using System.Collections;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using DCL.Optimization.PerformanceBudgeting;
using DCL.Profiling;
using ECS.StreamableLoading.Cache;
using ECS.StreamableLoading.Common.Components;
using ECS.Unity.GLTFContainer.Asset.Components;
using UnityEngine;
using Utility;
using Utility.Multithreading;
using Utility.PriorityQueue;

public class LODCache : IStreamableCache<LODAsset, string>
{
    internal readonly Dictionary<string, LODAsset> cache;
    private readonly Transform parentContainer;
    private readonly SimplePriorityQueue<string, long> unloadQueue = new();
    private IStreamableCache<LODAsset, string> streamableCacheImplementation;

    public IDictionary<string, UniTaskCompletionSource<StreamableLoadingResult<LODAsset>?>> OngoingRequests { get; }
    public IDictionary<string, StreamableLoadingResult<LODAsset>> IrrecoverableFailures { get; }

    public LODCache()
    {
        parentContainer = new GameObject("POOL_CONTAINER_LodCache").transform;
        parentContainer.gameObject.SetActive(false);

        cache = new Dictionary<string, LODAsset>();
    }


    public void Dispose()
    {
    }

    public bool TryGet(in string key, out LODAsset asset)
    {
        if (key != null && cache.TryGetValue(key, out asset))
        {
            ProfilingCounters.LODInstantiatedInCache.Value--;
            asset.Root.SetActive(true);
            asset.Root.transform.SetParent(null);
            return true;
        }

        asset = default;
        return false;
    }

    public void Add(in string key, LODAsset asset)
    {
        //Dont I need to add it to the cache somehow?
    }

    public void Dereference(in string key, LODAsset asset)
    {
        if (key == null)
            return;

        if (!TryGet(key, out var cachedAsset))
        {
            cache[key] = asset;
            unloadQueue.Enqueue(key, MultithreadingUtility.FrameCount);
        }

        unloadQueue.TryUpdatePriority(key, MultithreadingUtility.FrameCount);

        ProfilingCounters.LODInstantiatedInCache.Value++;

        // This logic should not be executed if the application is quitting
        if (UnityObjectUtils.IsQuitting) return;

        asset.Root.SetActive(false);
        asset.Root.transform.SetParent(parentContainer);
    }

    public void Unload(IConcurrentBudgetProvider frameTimeBudgetProvider, int maxUnloadAmount)
    {
        var unloadedAmount = 0;

        while (frameTimeBudgetProvider.TrySpendBudget()
               && unloadedAmount < maxUnloadAmount && unloadQueue.Count > 0
               && unloadQueue.TryDequeue(out var key) && cache.TryGetValue(key, out var asset))
        {
            unloadedAmount++;
            asset.Dispose();
            cache.Remove(key);
        }

        ProfilingCounters.LODInstantiatedInCache.Value -= unloadedAmount;
    }

    bool IEqualityComparer<string>.Equals(string x, string y)
    {
        return string.Equals(x, y, StringComparison.OrdinalIgnoreCase);
    }

    int IEqualityComparer<string>.GetHashCode(string obj)
    {
        return obj.GetHashCode(StringComparison.OrdinalIgnoreCase);
    }
}