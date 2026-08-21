using DCL.AvatarRendering.Emotes;
using DCL.AvatarRendering.Wearables.Components;
using DCL.AvatarRendering.Wearables.Helpers;
using DCL.Optimization.PerformanceBudgeting;
using ECS.StreamableLoading;
using ECS.StreamableLoading.Common.Components;
using ECS.StreamableLoading.Textures;
using NSubstitute;
using NUnit.Framework;

namespace DCL.ResourcesUnloading.Tests
{
    /// <summary>
    ///     Storage Unload must evict entries whose terminal thumbnail result never acquired a
    ///     refcounted reference (failed/cancelled carry a default <see cref="SpriteData" />),
    ///     and must release exactly one reference for succeeded thumbnails.
    /// </summary>
    public class StorageThumbnailDisposalShould
    {
        private const string WEARABLE_URN = "urn:decentraland:matic:collections-v2:0xstoragetest:1";
        private const string EMOTE_URN = "urn:decentraland:matic:collections-v2:0xstoragetest:2";

        private IReleasablePerformanceBudget budget = null!;

        [SetUp]
        public void SetUp()
        {
            budget = Substitute.For<IReleasablePerformanceBudget>();
            budget.TrySpendBudget().Returns(true);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void EvictTrimmedWearableWithUnacquiredThumbnail(bool cancelled)
        {
            var storage = new TrimmedWearableStorage();
            ITrimmedWearable wearable = storage.GetOrAddByDTO(TrimmedWearableDto(WEARABLE_URN));
            wearable.ThumbnailAssetResult = UnacquiredThumbnail(cancelled);

            Assert.DoesNotThrow(() => storage.Unload(budget));

            Assert.That(storage.TryGetElement(WEARABLE_URN, out _), Is.False);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void EvictWearableWithUnacquiredThumbnail(bool cancelled)
        {
            var storage = new WearableStorage();
            IWearable wearable = storage.AddWearable(WEARABLE_URN, new Wearable(), qualifiedForUnloading: true);
            wearable.ThumbnailAssetResult = UnacquiredThumbnail(cancelled);

            Assert.DoesNotThrow(() => storage.Unload(budget));

            Assert.That(storage.TryGetElement(WEARABLE_URN, out _), Is.False);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void EvictEmoteWithUnacquiredThumbnail(bool cancelled)
        {
            var storage = new MemoryEmotesStorage();
            IEmote emote = storage.GetOrAddByDTO(EmoteDto(EMOTE_URN));
            emote.ThumbnailAssetResult = UnacquiredThumbnail(cancelled);

            Assert.DoesNotThrow(() => storage.Unload(budget));

            Assert.That(storage.TryGetElement(EMOTE_URN, out _), Is.False);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void EvictTrimmedEmoteWithUnacquiredThumbnail(bool cancelled)
        {
            var storage = new TrimmedEmoteStorage();
            ITrimmedEmote emote = storage.GetOrAddByDTO(TrimmedEmoteDto(EMOTE_URN));
            emote.ThumbnailAssetResult = UnacquiredThumbnail(cancelled);

            Assert.DoesNotThrow(() => storage.Unload(budget));

            Assert.That(storage.TryGetElement(EMOTE_URN, out _), Is.False);
        }

        [Test]
        public void DereferenceSucceededThumbnailExactlyOnceOnUnload()
        {
            var trimmedWearableSpy = new RefCountSpy();
            var trimmedWearableStorage = new TrimmedWearableStorage();
            trimmedWearableStorage.GetOrAddByDTO(TrimmedWearableDto(WEARABLE_URN)).ThumbnailAssetResult = SucceededThumbnail(trimmedWearableSpy);

            var wearableSpy = new RefCountSpy();
            var wearableStorage = new WearableStorage();
            wearableStorage.AddWearable(WEARABLE_URN, new Wearable(), qualifiedForUnloading: true).ThumbnailAssetResult = SucceededThumbnail(wearableSpy);

            var emoteSpy = new RefCountSpy();
            var emoteStorage = new MemoryEmotesStorage();
            emoteStorage.GetOrAddByDTO(EmoteDto(EMOTE_URN)).ThumbnailAssetResult = SucceededThumbnail(emoteSpy);

            var trimmedEmoteSpy = new RefCountSpy();
            var trimmedEmoteStorage = new TrimmedEmoteStorage();
            trimmedEmoteStorage.GetOrAddByDTO(TrimmedEmoteDto(EMOTE_URN)).ThumbnailAssetResult = SucceededThumbnail(trimmedEmoteSpy);

            trimmedWearableStorage.Unload(budget);
            wearableStorage.Unload(budget);
            emoteStorage.Unload(budget);
            trimmedEmoteStorage.Unload(budget);

            Assert.That(trimmedWearableSpy.Dereferences, Is.EqualTo(1), nameof(TrimmedWearableStorage));
            Assert.That(wearableSpy.Dereferences, Is.EqualTo(1), nameof(WearableStorage));
            Assert.That(emoteSpy.Dereferences, Is.EqualTo(1), nameof(MemoryEmotesStorage));
            Assert.That(trimmedEmoteSpy.Dereferences, Is.EqualTo(1), nameof(TrimmedEmoteStorage));
        }

        private static StreamableLoadingResult<SpriteData>.WithFallback UnacquiredThumbnail(bool cancelled) =>
            cancelled
                ? StreamableLoadingResult<SpriteData>.WithFallback.CancelledResult()
                : StreamableLoadingResult<SpriteData>.WithFallback.Failed();

        private static StreamableLoadingResult<SpriteData>.WithFallback SucceededThumbnail(RefCountSpy spy) =>
            new (new SpriteData(spy, null!));

        private static TrimmedWearableDTO TrimmedWearableDto(string urn) =>
            new () { metadata = new TrimmedWearableDTO.WearableMetadataDto { id = urn } };

        private static TrimmedEmoteDTO TrimmedEmoteDto(string urn) =>
            new () { metadata = new TrimmedEmoteDTO.EmoteMetadataDto { id = urn } };

        private static EmoteDTO EmoteDto(string urn) =>
            new () { metadata = new EmoteDTO.EmoteMetadataDto { id = urn } };

        private class RefCountSpy : IStreamableRefCountData
        {
            public int Dereferences { get; private set; }

            public void Dereference()
            {
                Dereferences++;
            }

            public void Dispose() { }
        }
    }
}
