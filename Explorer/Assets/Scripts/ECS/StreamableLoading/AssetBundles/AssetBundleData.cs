using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Optimization.Memory;
using DCL.Profiling;
using DCL.WebRequests;
using System;
using System.IO;
using System.Threading;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Assertions;
using Object = UnityEngine.Object;

namespace ECS.StreamableLoading.AssetBundles
{
    /// <summary>
    ///     A wrapper over <see cref="AssetBundle" /> to provide additional data
    /// </summary>
    public class AssetBundleData : StreamableRefCountData<AssetBundle>
    {
        /// <summary>
        /// Keeps ownership of MemoryChain because its lifetime has to be aligned with the asset bundle
        /// </summary>
        public class InMemoryAssetBundle
        {
            private const string METADATA_FILENAME = "metadata.json";
            private const string METRICS_FILENAME = "metrics.json";

            internal readonly AssetBundle bundle;
            private readonly Stream stream;
            private bool unloaded;

            private InMemoryAssetBundle(AssetBundle bundle, Stream stream)
            {
                this.bundle = bundle;
                this.stream = stream;
                unloaded = false;
            }

            public bool IsEmpty => bundle == null;

            public static async UniTask<InMemoryAssetBundle> NewAsync(MemoryChain memoryChain, string key)
            {
                using var memoryStream = memoryChain.ToStream();

                string path = Path.Combine(Application.persistentDataPath!, key);
                path = Path.ChangeExtension(path, "ab");
                var fileStream = new FileStream(path, FileMode.Create, FileAccess.ReadWrite);
                await memoryStream.CopyToAsync(fileStream)!;

                await UniTask.SwitchToMainThread();
                var assetBundle = await AssetBundle.LoadFromStreamAsync(fileStream)!;

                return new InMemoryAssetBundle(assetBundle, fileStream);
            }

            public static InMemoryAssetBundle FromAssetBundle(AssetBundle assetBundle) =>
                new (assetBundle, Stream.Null!);

            public async UniTask UnloadAsync(bool unloadAllLoadedObjects)
            {
                if (unloaded)
                    return;

                unloaded = true;
                await UniTask.SwitchToMainThread();
                if (bundle) await bundle.UnloadAsync(unloadAllLoadedObjects)!;
                stream.Dispose();
            }

            public async UniTask<(string? metrics, string? metadata)> MetricsAndMetadataJsonAsync(AssetBundleLoadingMutex loadingMutex, CancellationToken ct)
            {
                if (unloaded)
                    throw new Exception("Already unloaded");

                string? metricsJson;
                string? metadataJson;

                using (AssetBundleLoadingMutex.LoadingRegion _ = await loadingMutex.AcquireAsync(ct))
                {
                    metricsJson = bundle.LoadAsset<TextAsset>(METRICS_FILENAME)?.text;
                    metadataJson = bundle.LoadAsset<TextAsset>(METADATA_FILENAME)?.text;
                }

                return (metricsJson, metadataJson);
            }
        }

        private readonly Object? mainAsset;
        private readonly Type? assetType;
        private readonly InMemoryAssetBundle? inMemoryAssetBundle;

        public readonly AssetBundleData[] Dependencies;

        public readonly AssetBundleMetrics? Metrics;

        private readonly string? description;

        public AssetBundleData(InMemoryAssetBundle? assetBundle, AssetBundleMetrics? metrics, Object mainAsset, Type assetType, AssetBundleData[] dependencies,
            string version = "", string source = "")
            : base(assetBundle?.bundle!, ReportCategory.ASSET_BUNDLES)
        {
            Metrics = metrics;

            this.mainAsset = mainAsset;
            Dependencies = dependencies;
            this.assetType = assetType;

            description = $"AB:{Asset?.name}_{version}_{source}";

            this.inMemoryAssetBundle = assetBundle;
        }

        /// <summary>
        ///     Constructor for dependencies (with the unknown asset type)
        /// </summary>
        internal AssetBundleData(InMemoryAssetBundle assetBundle, AssetBundleMetrics? metrics, AssetBundleData[] dependencies) : base(assetBundle.bundle, ReportCategory.ASSET_BUNDLES)
        {
            // Dependencies cant be unloaded, since we don't know who will need them =(
            Metrics = metrics;

            this.mainAsset = null;
            this.assetType = null;
            this.inMemoryAssetBundle = assetBundle;
            Dependencies = dependencies;
        }

        public AssetBundleData(InMemoryAssetBundle? assetBundle, AssetBundleMetrics? metrics, GameObject mainAsset, AssetBundleData[] dependencies)
            : this(assetBundle, metrics, mainAsset, typeof(GameObject), dependencies) { }

        protected override ref ProfilerCounterValue<int> totalCount => ref ProfilingCounters.ABDataAmount;

        protected override ref ProfilerCounterValue<int> referencedCount => ref ProfilingCounters.ABReferencedAmount;

        //We immediately unload the asset bundle, as we don't need it anymore.
        //Very hacky, because the asset will remain in cache as AssetBundle == null
        //When DestroyObject is invoked, it will do nothing.
        //When cache in cleaned, the AssetBundleData will be removed from the list. Its there doing nothing
        internal UniTask UnloadABAsync() =>
            inMemoryAssetBundle?.UnloadAsync(false) ?? UniTask.CompletedTask;

        protected override void DestroyObject()
        {
            foreach (AssetBundleData child in Dependencies)
                child.Dereference();

            if (mainAsset != null)
                Object.DestroyImmediate(mainAsset, true);

            inMemoryAssetBundle?.UnloadAsync(true).Forget();
        }

        public T GetMainAsset<T>() where T: Object
        {
            Assert.IsNotNull(assetType, "GetMainAsset can't be called on the Asset Bundle that was not loaded with the asset type specified");

            if (assetType != typeof(T))
                throw new ArgumentException("Asset type mismatch: " + typeof(T) + " != " + assetType);

            return (T)mainAsset!;
        }

        public string GetInstanceName() =>
            description ?? string.Empty;
    }
}
