using Arch.Core;
using Cysharp.Threading.Tasks;
using DCL.AvatarRendering.Loading.Components;
using DCL.AvatarRendering.Loading.DTO;
using DCL.AvatarRendering.Loading.Exceptions;
using DCL.AvatarRendering.Thumbnails.Systems;
using DCL.AvatarRendering.Thumbnails.Utils;
using DCL.AvatarRendering.Wearables;
using DCL.AvatarRendering.Wearables.Helpers;
using DCL.Diagnostics;
using DCL.Ipfs;
using DCL.Multiplayer.Connections.DecentralandUrls;
using ECS.Prioritization.Components;
using ECS.StreamableLoading.AssetBundles;
using ECS.StreamableLoading.Common.Components;
using ECS.StreamableLoading.Textures;
using NSubstitute;
using NUnit.Framework;
using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using Promise = ECS.StreamableLoading.Common.AssetPromise<ECS.StreamableLoading.Textures.TextureData, ECS.StreamableLoading.Textures.GetTextureIntention>;
using AssetBundlePromise = ECS.StreamableLoading.Common.AssetPromise<ECS.StreamableLoading.AssetBundles.AssetBundleData, ECS.StreamableLoading.AssetBundles.GetAssetBundleIntention>;

namespace DCL.AvatarRendering.AvatarShape.Tests
{
    [TestFixture]
    public class ECSThumbnailProviderShould
    {
        private static readonly QueryDescription THUMBNAIL_PROMISES = new QueryDescription().WithAll<IThumbnailAttachment, Promise>();
        private static readonly int[] NO_RETRY_DELAY = { 0 };

        public enum AttachmentKind
        {
            Wearable,
            TrimmedWearable,
        }

        private World world = null!;
        private ECSThumbnailProvider provider = null!;
        private ResolveAvatarAttachmentThumbnailSystem resolver = null!;
        private Texture2D downloadedTexture = null!;

        [SetUp]
        public void SetUp()
        {
            world = World.Create();

            IDecentralandUrlsSource urlsSource = Substitute.For<IDecentralandUrlsSource>();
            urlsSource.Url(Arg.Any<DecentralandUrl>()).Returns("https://peer.decentraland.test/content");

            provider = new ECSThumbnailProvider(urlsSource, world);
            resolver = new ResolveAvatarAttachmentThumbnailSystem(world);
            downloadedTexture = new Texture2D(4, 4);
        }

        [TearDown]
        public void TearDown()
        {
            world.Dispose();
            UnityEngine.Object.DestroyImmediate(downloadedTexture);
        }

        [Test]
        public async Task KeepAFailedDownloadRetryableForTheNextCaller()
        {
            FakeWearable wearable = NewWearable();

            UniTask<Sprite> firstAttempt = provider.GetAsync(wearable, CancellationToken.None);
            ResolvePendingDownload(FailedDownload());
            Sprite firstSprite = await firstAttempt;

            Assert.That(firstSprite, Is.SameAs(LoadThumbnailsUtils.DEFAULT_THUMBNAIL.Sprite), "A failed download shows the default thumbnail");
            Assert.That(wearable.ThumbnailAssetResult!.Value.Succeeded, Is.False, "A failed download must not be cached as a loaded thumbnail");

            UniTask<Sprite> nextAttempt = provider.GetAsync(wearable, CancellationToken.None);
            Assert.That(world.CountEntities(in THUMBNAIL_PROMISES), Is.EqualTo(1), "The next caller must download the thumbnail again");

            ResolvePendingDownload(SucceededDownload());
            Assert.That((await nextAttempt).texture, Is.SameAs(downloadedTexture));
        }

        [Test]
        public async Task KeepRetryingAVisibleThumbnailUntilADownloadSucceeds()
        {
            FakeWearable wearable = NewWearable();
            Sprite? shown = null;

            UniTask loading = provider.LoadWithRetriesAsync(wearable, sprite => shown = sprite, CancellationToken.None, NO_RETRY_DELAY);

            ResolvePendingDownload(FailedDownload());
            Assert.That(await PendingDownloadAppearsAsync(), Is.True, "A visible thumbnail must download again after a failed attempt");
            Assert.That(shown, Is.SameAs(LoadThumbnailsUtils.DEFAULT_THUMBNAIL.Sprite), "The failed attempt shows the default thumbnail while the retry runs");

            ResolvePendingDownload(SucceededDownload());
            await loading;

            Assert.That(shown!.texture, Is.SameAs(downloadedTexture));
        }

        [Test]
        public async Task StopRetryingAVisibleThumbnailOnceItsViewCloses()
        {
            FakeWearable wearable = NewWearable();
            using var viewLifetime = new CancellationTokenSource();

            UniTask loading = provider.LoadWithRetriesAsync(wearable, _ => { }, viewLifetime.Token, NO_RETRY_DELAY);

            ResolvePendingDownload(FailedDownload());
            Assert.That(await PendingDownloadAppearsAsync(), Is.True, "A visible thumbnail must download again after a failed attempt");

            viewLifetime.Cancel();
            resolver.Update(0);

            var cancelled = false;

            try { await loading; }
            catch (OperationCanceledException) { cancelled = true; }

            Assert.That(cancelled, Is.True, "Closing the view must end the retries");
            Assert.That(await PendingDownloadAppearsAsync(), Is.False, "A closed view must not download its thumbnail again");
        }

        [Test]
        public async Task StopRetryingAVisibleThumbnailAfterTheLastDelay()
        {
            FakeWearable wearable = NewWearable();
            Sprite? shown = null;
            using var viewLifetime = new CancellationTokenSource();

            UniTask loading = provider.LoadWithRetriesAsync(wearable, sprite => shown = sprite, viewLifetime.Token, NO_RETRY_DELAY);

            try
            {
                ResolvePendingDownload(FailedDownload());
                Assert.That(await PendingDownloadAppearsAsync(), Is.True, "A visible thumbnail must download again after a failed attempt");

                ResolvePendingDownload(FailedDownload());
                Assert.That(await PendingDownloadAppearsAsync(), Is.False, "A visible thumbnail must stop downloading once its retry delays run out");
                Assert.That(loading.Status, Is.EqualTo(UniTaskStatus.Succeeded), "The load must end after its last retry");
                Assert.That(shown, Is.SameAs(LoadThumbnailsUtils.DEFAULT_THUMBNAIL.Sprite), "A load that gave up keeps showing the default thumbnail");
            }
            finally
            {
                // Ends a loop that kept retrying, so it does not outlive the test
                viewLifetime.Cancel();
                resolver.Update(0);
            }
        }

        [Test]
        public void StoreAFailedBundleDownloadAsAFailureCarryingTheDefaultThumbnail()
        {
            FakeWearable wearable = NewWearable();

            ResolveBundleDownload(wearable, new StreamableLoadingResult<AssetBundleData>(ReportData.UNSPECIFIED, new Exception("Simulated aborted bundle download")));

            Assert.That(wearable.ThumbnailAssetResult!.Value.IsInitialized, Is.True);
            Assert.That(wearable.ThumbnailAssetResult!.Value.Succeeded, Is.False, "A failed bundle download must not be cached as a loaded thumbnail");
            Assert.That(wearable.ThumbnailAssetResult!.Value.Asset.Sprite, Is.SameAs(LoadThumbnailsUtils.DEFAULT_THUMBNAIL.Sprite));
        }

        [Test]
        public void StoreABundleWithoutATextureAsAFailureCarryingTheDefaultThumbnail()
        {
            FakeWearable wearable = NewWearable();

            ResolveBundleDownload(wearable, new StreamableLoadingResult<AssetBundleData>(LoadedBundleWithoutATexture()));

            Assert.That(wearable.ThumbnailAssetResult!.Value.IsInitialized, Is.True);
            Assert.That(wearable.ThumbnailAssetResult!.Value.Succeeded, Is.False, "A bundle that holds no texture must not be cached as a loaded thumbnail");
            Assert.That(wearable.ThumbnailAssetResult!.Value.Asset.Sprite, Is.SameAs(LoadThumbnailsUtils.DEFAULT_THUMBNAIL.Sprite));
        }

        [Test]
        public void ReleaseABundleThatHoldsNoTexture()
        {
            FakeWearable wearable = NewWearable();
            AssetBundleData emptyBundle = LoadedBundleWithoutATexture();

            ResolveBundleDownload(wearable, new StreamableLoadingResult<AssetBundleData>(emptyBundle));

            Assert.That(emptyBundle.CanBeDisposed(), Is.True, "The bundle reference taken on load must be released when no thumbnail keeps it");
        }

        [Test]
        public async Task GiveTrimmedAttachmentsTheDefaultThumbnailWhenTheDownloadFails()
        {
            FakeTrimmedWearable trimmed = NewTrimmedWearable();

            UniTask<Sprite> attempt = provider.GetAsync(trimmed, CancellationToken.None);
            ResolvePendingDownload(FailedDownload());

            Sprite? sprite;

            try { sprite = await attempt; }
            catch (ThumbnailLoadFailedException) { sprite = null; }

            Assert.That(sprite, Is.SameAs(LoadThumbnailsUtils.DEFAULT_THUMBNAIL.Sprite), "A failed download of a trimmed attachment must show the default thumbnail, not throw");
        }

        [Test]
        public async Task GiveJoinedCallersTheDefaultThumbnailWhenTheLoadTimesOut()
        {
            FakeWearable wearable = NewWearable();

            // Long enough that the timeout cannot fire before the second caller joins; otherwise that caller starts its own load and waits out the default timeout
            UniTask<Sprite> starting = provider.GetAsync(wearable, CancellationToken.None, timeoutMs: 100);
            Assert.That(starting.Status, Is.EqualTo(UniTaskStatus.Pending), "The starting caller must still be loading when the second caller joins");

            UniTask<Sprite> joined = provider.GetAsync(wearable, CancellationToken.None);
            await starting;

            Assert.That(await joined, Is.SameAs(LoadThumbnailsUtils.DEFAULT_THUMBNAIL.Sprite), "A caller sharing a timed-out load must get the default thumbnail, not a missing sprite");
        }

        [Test]
        public async Task LeaveASharedLoadRunningWhenAJoinedCallerTimesOut()
        {
            FakeWearable wearable = NewWearable();

            UniTask<Sprite> starting = provider.GetAsync(wearable, CancellationToken.None);
            Sprite joinedSprite = await provider.GetAsync(wearable, CancellationToken.None, timeoutMs: 1);

            Assert.That(joinedSprite, Is.SameAs(LoadThumbnailsUtils.DEFAULT_THUMBNAIL.Sprite), "A joined caller that times out shows the default thumbnail");
            Assert.That(wearable.ThumbnailAssetResult is { IsInitialized: false }, Is.True, "A joined caller's timeout must not end the load it joined");

            ResolvePendingDownload(SucceededDownload());
            Assert.That((await starting).texture, Is.SameAs(downloadedTexture), "The caller that started the load must still get the downloaded thumbnail");
        }

        [TestCase(AttachmentKind.Wearable)]
        [TestCase(AttachmentKind.TrimmedWearable)]
        public async Task GiveJoinedCallersTheDefaultThumbnailWhenTheStartingCallerCancelsTheLoad(AttachmentKind kind)
        {
            IThumbnailAttachment attachment = kind == AttachmentKind.Wearable ? NewWearable() : NewTrimmedWearable();
            using var startingCallerLifetime = new CancellationTokenSource();

            UniTask<Sprite> starting = provider.GetAsync(attachment, startingCallerLifetime.Token);
            UniTask<Sprite> joined = provider.GetAsync(attachment, CancellationToken.None);

            startingCallerLifetime.Cancel();
            resolver.Update(0);
            Assert.That(attachment.ThumbnailAssetResult is { Cancelled: true }, Is.True, "The resolver stamps the cancelled load on the shared slot");

            try { await starting; }
            catch (OperationCanceledException) { }

            Sprite? sprite;

            try { sprite = await joined; }
            catch (ThumbnailLoadFailedException) { sprite = null; }

            Assert.That(sprite, Is.SameAs(LoadThumbnailsUtils.DEFAULT_THUMBNAIL.Sprite), "A caller whose shared load another caller cancelled must get the default thumbnail");
        }

        [Test]
        public async Task RetryAfterFailedSlotInsteadOfRethrowing()
        {
            FakeWearable wearable = NewWearable();
            wearable.ThumbnailAssetResult = StreamableLoadingResult<SpriteData>.WithFallback.Failed(LoadThumbnailsUtils.DEFAULT_THUMBNAIL);

            UniTask<Sprite> getTask = provider.GetAsync(wearable, CancellationToken.None);
            int promisesSpawned = world.CountEntities(in THUMBNAIL_PROMISES);

            // Resolve the attachment the way ResolveAvatarAttachmentThumbnailSystem does on success.
            SpriteData spriteData = NewSpriteData();
            wearable.ThumbnailAssetResult = new StreamableLoadingResult<SpriteData>.WithFallback(spriteData);

            Sprite? sprite;

            try { sprite = await getTask; }
            catch (ThumbnailLoadFailedException) { sprite = null; }

            Assert.That(promisesSpawned, Is.EqualTo(1), "GetAsync must clear a Failed slot and spawn a fresh promise instead of rethrowing the cached failure");
            Assert.That(sprite, Is.SameAs(spriteData.Sprite));
        }

        [Test]
        public async Task ReturnCachedSuccessWithoutSpawningPromise()
        {
            FakeWearable wearable = NewWearable();
            SpriteData spriteData = NewSpriteData();
            wearable.ThumbnailAssetResult = new StreamableLoadingResult<SpriteData>.WithFallback(spriteData);

            Sprite sprite = await provider.GetAsync(wearable, CancellationToken.None);

            Assert.That(sprite, Is.SameAs(spriteData.Sprite));
            Assert.That(world.CountEntities(in THUMBNAIL_PROMISES), Is.Zero);
        }

        [Test]
        public async Task MarkFailedOnTimeoutAndRetryOnNextCall()
        {
            FakeWearable wearable = NewWearable();

            Sprite firstResult = await provider.GetAsync(wearable, CancellationToken.None, timeoutMs: 1);

            Assert.That(firstResult, Is.SameAs(LoadThumbnailsUtils.DEFAULT_THUMBNAIL.Sprite), "A timed-out load must resolve to the default thumbnail instead of throwing");
            Assert.That(wearable.ThumbnailAssetResult, Is.Not.Null);
            Assert.That(wearable.ThumbnailAssetResult!.Value.IsInitialized, Is.True);
            Assert.That(wearable.ThumbnailAssetResult!.Value.Succeeded, Is.False);

            int promisesAfterTimeout = world.CountEntities(in THUMBNAIL_PROMISES);

            UniTask<Sprite> retryTask = provider.GetAsync(wearable, CancellationToken.None, timeoutMs: 1);
            int promisesAfterRetry = world.CountEntities(in THUMBNAIL_PROMISES);

            Sprite retryResult = await retryTask;

            Assert.That(retryResult, Is.SameAs(LoadThumbnailsUtils.DEFAULT_THUMBNAIL.Sprite));
            Assert.That(promisesAfterRetry, Is.EqualTo(promisesAfterTimeout + 1), "A call after a timed-out attempt must spawn a fresh promise instead of rethrowing the cached failure");
        }

        private static FakeWearable NewWearable() =>
            new (new WearableDTO
            {
                metadata = new WearableDTO.WearableMetadataDto
                {
                    id = "urn:decentraland:off-chain:base-avatars:red_hoodie",
                    thumbnail = "bafybeie7lzqakerm4n4x7557g3va4sv7aeoniexlomdgjskuoubo6s3mku",
                    data =
                    {
                        representations = new AvatarAttachmentDTO.Representation[] { new () },
                    },
                },
            });

        private static FakeTrimmedWearable NewTrimmedWearable() =>
            new (new TrimmedWearableDTO
            {
                id = "urn:decentraland:off-chain:base-avatars:red_hoodie",
                thumbnail = "bafybeie7lzqakerm4n4x7557g3va4sv7aeoniexlomdgjskuoubo6s3mku",
                metadata = new TrimmedWearableDTO.WearableMetadataDto { id = "urn:decentraland:off-chain:base-avatars:red_hoodie" },
            });

        /// <summary>
        ///     A bundle that loaded but holds no texture, carrying the reference that LoadSystemBase takes on a successful load.
        /// </summary>
        private static AssetBundleData LoadedBundleWithoutATexture()
        {
            var bundle = new AssetBundleData(null, Array.Empty<UnityEngine.Object>(), null, Array.Empty<AssetBundleData>());
            bundle.AddReference();
            return bundle;
        }

        private static SpriteData NewSpriteData() =>
            new (new TextureData(Texture2D.whiteTexture), Sprite.Create(Texture2D.whiteTexture!, new Rect(0, 0, 1, 1), new Vector2()));

        private static StreamableLoadingResult<TextureData> FailedDownload() =>
            new (ReportData.UNSPECIFIED, new Exception("Simulated aborted thumbnail download"));

        private StreamableLoadingResult<TextureData> SucceededDownload() =>
            new (new TextureData(downloadedTexture));

        /// <summary>
        ///     Completes the thumbnail download in flight the way the loading pipeline does, then lets the real resolver system consume it.
        /// </summary>
        private void ResolvePendingDownload(StreamableLoadingResult<TextureData> download)
        {
            Promise? pending = null;
            world.Query(in THUMBNAIL_PROMISES, (ref Promise promise) => { pending = promise; });

            Assert.That(pending, Is.Not.Null, "No thumbnail download is in flight");

            world.Add(pending!.Value.Entity, download);
            resolver.Update(0);
        }

        /// <summary>
        ///     Starts a thumbnail bundle download, completes it the way the loading pipeline does, then lets the real resolver system consume it.
        /// </summary>
        private void ResolveBundleDownload(IThumbnailAttachment attachment, StreamableLoadingResult<AssetBundleData> download)
        {
            attachment.ThumbnailAssetResult = new StreamableLoadingResult<SpriteData>.WithFallback();

            var promise = AssetBundlePromise.Create(world,
                GetAssetBundleIntention.FromHash("bafybeie7lzqakerm4n4x7557g3va4sv7aeoniexlomdgjskuoubo6s3mku_windows", AssetBundleManifestVersion.CreateManualManifest(), typeof(Texture2D)),
                PartitionComponent.TOP_PRIORITY);

            world.Create(attachment, promise, (IPartitionComponent)PartitionComponent.TOP_PRIORITY);
            world.Add(promise.Entity, download);
            resolver.Update(0);
        }

        private async UniTask<bool> PendingDownloadAppearsAsync()
        {
            const int MAX_FRAMES_FOR_PENDING_DOWNLOAD = 120;

            for (var frame = 0; frame < MAX_FRAMES_FOR_PENDING_DOWNLOAD; frame++)
            {
                if (world.CountEntities(in THUMBNAIL_PROMISES) > 0)
                    return true;

                await UniTask.Yield();
            }

            return false;
        }
    }
}
