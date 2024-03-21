using DCL.Profiling;
using JetBrains.Annotations;
using System;
using UnityEngine;
using UnityEngine.Assertions;
using Utility.Multithreading;
using Object = UnityEngine.Object;

namespace ECS.StreamableLoading.AssetBundles
{
    /// <summary>
    ///     A wrapper over <see cref="AssetBundle" /> to provide additional data
    /// </summary>
    public class AssetBundleData : IDisposable
    {
        private readonly Object mainAsset;
        private readonly Type assetType;

        public readonly AssetBundle AssetBundle;
        public readonly AssetBundleData[] Dependencies;

        public readonly AssetBundleMetrics? Metrics;

        internal int referencesCount;
        public long LastUsedFrame { get; private set; }

        public AssetBundleData(AssetBundle assetBundle, AssetBundleMetrics? metrics, Object mainAsset, Type assetType, AssetBundleData[] dependencies)
        {
            AssetBundle = assetBundle;
            Metrics = metrics;

            this.mainAsset = mainAsset;
            Dependencies = dependencies;
            this.assetType = assetType;

            ProfilingCounters.ABDataAmount.Value++;
        }

        public void Dispose()
        {
            if (!CanBeDisposed()) return;

            if (AssetBundle != null)
                AssetBundle.UnloadAsync(unloadAllLoadedObjects: true);

            if (referencesCount > 0)
                ProfilingCounters.ABReferencedAmount.Value--;

            ProfilingCounters.ABDataAmount.Value--;
        }

        public T? GetMainAsset<T>() where T : Object
        {
            if (assetType != typeof(T))
                throw new ArgumentException("Asset type mismatch: " + typeof(T) + " != " + assetType);

            return (T)mainAsset;
        }

        public bool CanBeDisposed() =>
            referencesCount == 0;

        public void AddReference()
        {
            referencesCount++;
            LastUsedFrame = MultithreadingUtility.FrameCount;

            if (referencesCount == 1)
                ProfilingCounters.ABReferencedAmount.Value++;
        }

        public void Dereference()
        {
            referencesCount--;
            LastUsedFrame = MultithreadingUtility.FrameCount;

            Assert.IsFalse(referencesCount < 0, "References count of asset bundle cannot be less then zero!");

            if (referencesCount == 0)
                ProfilingCounters.ABReferencedAmount.Value--;
        }
    }
}
