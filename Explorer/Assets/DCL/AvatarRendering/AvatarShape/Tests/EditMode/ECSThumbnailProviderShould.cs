using Arch.Core;
using Cysharp.Threading.Tasks;
using DCL.AvatarRendering.Loading.Components;
using DCL.AvatarRendering.Loading.DTO;
using DCL.AvatarRendering.Loading.Exceptions;
using DCL.AvatarRendering.Wearables;
using DCL.AvatarRendering.Wearables.Helpers;
using DCL.Multiplayer.Connections.DecentralandUrls;
using ECS.StreamableLoading.Common.Components;
using ECS.StreamableLoading.Textures;
using NSubstitute;
using NUnit.Framework;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using Promise = ECS.StreamableLoading.Common.AssetPromise<ECS.StreamableLoading.Textures.TextureData, ECS.StreamableLoading.Textures.GetTextureIntention>;

namespace DCL.AvatarRendering.AvatarShape.Tests
{
    [TestFixture]
    public class ECSThumbnailProviderShould
    {
        private static readonly QueryDescription THUMBNAIL_PROMISES = new QueryDescription().WithAll<IThumbnailAttachment, Promise>();

        private World world = null!;
        private ECSThumbnailProvider provider = null!;

        [SetUp]
        public void SetUp()
        {
            world = World.Create();

            IDecentralandUrlsSource urlsSource = Substitute.For<IDecentralandUrlsSource>();
            urlsSource.Url(Arg.Any<DecentralandUrl>()).Returns("https://peer.decentraland.test/content");

            provider = new ECSThumbnailProvider(urlsSource, world);
        }

        [TearDown]
        public void TearDown()
        {
            world.Dispose();
        }

        [Test]
        public async Task RetryAfterFailedSlotInsteadOfRethrowing()
        {
            FakeWearable wearable = NewWearable();
            wearable.ThumbnailAssetResult = StreamableLoadingResult<SpriteData>.WithFallback.Failed();

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

            try
            {
                await provider.GetAsync(wearable, CancellationToken.None, timeoutMs: 1);
                Assert.Fail("Expected the first never-resolving load to throw after the timeout");
            }
            catch (ThumbnailLoadFailedException) { }

            Assert.That(wearable.ThumbnailAssetResult, Is.Not.Null);
            Assert.That(wearable.ThumbnailAssetResult!.Value.IsInitialized, Is.True);
            Assert.That(wearable.ThumbnailAssetResult!.Value.Succeeded, Is.False);

            int promisesAfterTimeout = world.CountEntities(in THUMBNAIL_PROMISES);

            UniTask<Sprite> retryTask = provider.GetAsync(wearable, CancellationToken.None, timeoutMs: 1);
            int promisesAfterRetry = world.CountEntities(in THUMBNAIL_PROMISES);

            try { await retryTask; }
            catch (ThumbnailLoadFailedException) { }

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

        private static SpriteData NewSpriteData() =>
            new (new TextureData(Texture2D.whiteTexture), Sprite.Create(Texture2D.whiteTexture!, new Rect(0, 0, 1, 1), new Vector2()));
    }
}
