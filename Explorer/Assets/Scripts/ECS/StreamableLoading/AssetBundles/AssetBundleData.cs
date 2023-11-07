using JetBrains.Annotations;
using NUnit.Framework;
using System;
using UnityEngine;

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

        private GameObject gameObject;
        private bool gameObjectLoaded;
        private int referencesCount;

        /// <summary>
        ///     Root assets - Game Objects
        /// </summary>
        public GameObject GameObject { get; }

        public AssetBundleData(AssetBundle assetBundle, [CanBeNull] AssetBundleMetrics? metrics, GameObject gameObject, AssetBundleData[] dependencies)
        {
            AssetBundle = assetBundle;
            Metrics = metrics;

            GameObject = gameObject;
            Dependencies = dependencies;
        }

        public void Dispose()
        {
            if (AssetBundle != null)
                AssetBundle.Unload(unloadAllLoadedObjects: true);
        }

        public bool CanBeDisposed() =>
            referencesCount == 0;

        public void AddReference()
        {
            referencesCount++;
        }

        public void Dereference()
        {
            referencesCount--;
            Assert.IsFalse(referencesCount < 0, "VV:: ReferencesCount < 0");
        }
    }
}
