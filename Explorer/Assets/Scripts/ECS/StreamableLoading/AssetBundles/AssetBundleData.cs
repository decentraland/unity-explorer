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
        public readonly AssetBundle AssetBundle;
        public readonly AssetBundleData[] Dependencies;

        [CanBeNull] public readonly AssetBundleMetrics? Metrics;

        internal int referencesCount;
        private Object MainAsset { get; }

        public long LastUsedFrame { get; private set; }

        public AssetBundleData(AssetBundle assetBundle, [CanBeNull] AssetBundleMetrics? metrics, Object mainAsset, AssetBundleData[] dependencies)
        {
            AssetBundle = assetBundle;
            Metrics = metrics;

            MainAsset = mainAsset;
            Dependencies = dependencies;

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
            return MainAsset as T;
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
