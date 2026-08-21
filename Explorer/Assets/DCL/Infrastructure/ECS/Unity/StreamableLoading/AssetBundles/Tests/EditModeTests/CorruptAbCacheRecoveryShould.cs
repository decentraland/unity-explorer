using Arch.Core;
using Cysharp.Threading.Tasks;
using DCL.WebRequests;
using ECS.Prioritization.Components;
using ECS.StreamableLoading.Cache;
using ECS.StreamableLoading.Cache.Disk;
using ECS.StreamableLoading.Common.Components;
using NSubstitute;
using NUnit.Framework;
using System;
using System.Buffers;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.TestTools;

namespace ECS.StreamableLoading.AssetBundles.Tests
{
    /// <summary>
    ///     Covers the recovery invariant for corrupt entries in Unity's built-in <see cref="Caching" />:
    ///     a corrupt cached archive completes its web request successfully (cache hit, no network) yet
    ///     yields a null bundle from the native mount ("Unable to open archive file"), and since nothing
    ///     evicts the entry, the bundle stays broken for the user across sessions. The load flow must
    ///     evict the entry and re-request once so the cache is re-populated with a whole archive.
    /// </summary>
    [TestFixture]
    public class CorruptAbCacheRecoveryShould
    {
        private const string BUNDLE_CACHE_NAME = "bafkreid3xecd44iujaz5qekbdrt5orqdqj3wivg5zc5mya3zkorjhyrkda";

        private static string bundlePath => $"{Application.dataPath}/../TestResources/AssetBundles/{BUNDLE_CACHE_NAME}";
        private static string bundleUrl => $"file://{bundlePath}";

        private World world = null!;
        private IStreamableCache<AssetBundleData, GetAssetBundleIntention> cache = null!;
        private ExposedLoadAssetBundleSystem? system;

        [SetUp]
        public void SetUp()
        {
            world = World.Create();
            cache = Substitute.For<IStreamableCache<AssetBundleData, GetAssetBundleIntention>>();
            Caching.ClearAllCachedVersions(BUNDLE_CACHE_NAME);
        }

        [TearDown]
        public void TearDown()
        {
            LogAssert.ignoreFailingMessages = false;
            AssetBundle.UnloadAllAssetBundles(true);
            Caching.ClearAllCachedVersions(BUNDLE_CACHE_NAME);
            system?.Dispose();
            world.Dispose();
        }

        [Test]
        public async Task RecoverFromCorruptCachedArchive()
        {
            // The native "Unable to open archive file" error raised by the corrupt mount is the expected symptom
            LogAssert.ignoreFailingMessages = true;

            Hash128 cacheHash = Hash128.Compute("corrupt-ab-cache-recovery");

            await SeedCacheAsync(cacheHash);

            string dataFile = FindCachedDataFile();
            byte[] original = File.ReadAllBytes(dataFile);
            var garbage = new byte[original.Length];

            for (var i = 0; i < garbage.Length; i++)
                garbage[i] = 0xAB;

            // Same-length corruption models the real defect: presence checks still hit, only the mount fails
            File.WriteAllBytes(dataFile, garbage);

            system = new ExposedLoadAssetBundleSystem(world, cache, IWebRequestController.TEST);

            // Without the eviction retry this await faults with NullReferenceException: the corrupt entry is
            // served from the cache, DownloadHandlerAssetBundle.GetContent returns null and no recovery exists,
            // while the corrupt entry stays cached for every future attempt.
            StreamableLoadingResult<AssetBundleData> result = await system.FlowAsync(
                NewWebIntention(bundleUrl, cacheHash), StreamableLoadingState.Create(), PartitionComponent.TOP_PRIORITY, CancellationToken.None);

            Assert.That(result.Succeeded, Is.True, result.Exception?.ToString());
            Assert.That(result.Asset, Is.Not.Null);

            // The corrupt entry was evicted and re-downloaded: the cached archive is whole again
            Assert.That(Caching.IsVersionCached(bundleUrl, cacheHash), Is.True);

            byte[] repaired = File.ReadAllBytes(FindCachedDataFile());
            Assert.That(repaired[..8], Is.EqualTo(original[..8]), "the cached archive must be re-populated with real content");

            await PumpAsync();
        }

        [Test]
        public async Task RetryExactlyOnceAfterEvictingWhenCachedRequestYieldsNullBundle()
        {
            LogAssert.ignoreFailingMessages = true;

            IWebRequestController controller = Substitute.For<IWebRequestController>();
            var requests = 0;
            AssetBundle? retryBundle = null;

            controller.SendAsync<GetAssetBundleWebRequest, GetAssetBundleArguments, GetAssetBundleWebRequest.CreateAssetBundleOp, AssetBundleLoadingResult>(
                           Arg.Any<RequestEnvelope<GetAssetBundleWebRequest, GetAssetBundleArguments>>(),
                           Arg.Any<GetAssetBundleWebRequest.CreateAssetBundleOp>(),
                           Arg.Any<long>(),
                           Arg.Any<IProgress<float>?>())
                      .Returns(_ =>
                       {
                           requests++;

                           // The first response models a corrupt cache-served archive: the request "succeeds", the bundle is null
                           if (requests == 1)
                               return UniTask.FromResult(new AssetBundleLoadingResult(null, "Unable to open archive file"));

                           retryBundle ??= AssetBundle.LoadFromFile(bundlePath);
                           return UniTask.FromResult(new AssetBundleLoadingResult(retryBundle, null));
                       });

            system = new ExposedLoadAssetBundleSystem(world, cache, controller);

            // Without the eviction retry this await faults with NullReferenceException after a single request
            StreamableLoadingResult<AssetBundleData> result = await system.FlowAsync(
                NewWebIntention("https://ab-cdn.decentraland.test/v49/assets/corrupt_stub_windows", Hash128.Compute("corrupt-stub")),
                StreamableLoadingState.Create(), PartitionComponent.TOP_PRIORITY, CancellationToken.None);

            Assert.That(requests, Is.EqualTo(2), "a cache-eligible null bundle must be followed by exactly one evict-and-retry re-request");
            Assert.That(result.Succeeded, Is.True, result.Exception?.ToString());
            Assert.That(result.Asset, Is.Not.Null);

            await PumpAsync();
        }

        private static GetAssetBundleIntention NewWebIntention(string url, Hash128 cacheHash) =>
            new (new CommonLoadingArguments(url, attempts: 1)) { cacheHash = cacheHash };

        private static async Task SeedCacheAsync(Hash128 cacheHash)
        {
            using UnityWebRequest webRequest = UnityWebRequestAssetBundle.GetAssetBundle(bundleUrl, cacheHash);
            UnityWebRequestAsyncOperation operation = webRequest.SendWebRequest();

            while (!operation.isDone)
                await UniTask.Yield();

            Assume.That(webRequest.result, Is.EqualTo(UnityWebRequest.Result.Success), "seeding Unity's AssetBundle cache failed");

            AssetBundle? seeded = DownloadHandlerAssetBundle.GetContent(webRequest);
            Assume.That(seeded, Is.Not.Null, "seeding Unity's AssetBundle cache produced no bundle");
            seeded!.Unload(true);

            Assume.That(Caching.IsVersionCached(bundleUrl, cacheHash), Is.True, "Editor Caching did not store the seeded bundle");
        }

        private static string FindCachedDataFile()
        {
            string bundleRoot = Path.Combine(Caching.currentCacheForWriting.path, BUNDLE_CACHE_NAME);
            Assume.That(Directory.Exists(bundleRoot), Is.True, $"no cache folder at {bundleRoot}");

            string[] entries = Directory.GetDirectories(bundleRoot);

            for (var i = 0; i < entries.Length; i++)
            {
                string candidate = Path.Combine(entries[i], "__data");

                if (File.Exists(candidate))
                    return candidate;
            }

            Assume.That(false, $"no cached __data found under {bundleRoot}");
            return null!;
        }

        private static async Task PumpAsync()
        {
            // Land detached continuations inside the test window instead of after it (EditMode harness requirement)
            for (var i = 0; i < 10; i++)
                await UniTask.Yield();
        }

        private class ExposedLoadAssetBundleSystem : LoadAssetBundleSystem
        {
            public ExposedLoadAssetBundleSystem(World world, IStreamableCache<AssetBundleData, GetAssetBundleIntention> cache, IWebRequestController webRequestController)
                : base(world, cache, webRequestController, ArrayPool<byte>.Shared, new AssetBundleLoadingMutex(), Substitute.For<IDiskCache<PartialLoadingState>>(), byteWeightedProgress: false) { }

            public UniTask<StreamableLoadingResult<AssetBundleData>> FlowAsync(GetAssetBundleIntention intention, StreamableLoadingState state, IPartitionComponent partition, CancellationToken ct) =>
                FlowInternalAsync(intention, state, partition, ct);
        }
    }
}
