using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.AvatarRendering.Emotes;
using DCL.AvatarRendering.Emotes.Equipped;
using DCL.AvatarRendering.Loading.Components;
using DCL.AvatarRendering.Thumbnails.Utils;
using DCL.AvatarRendering.Wearables;
using DCL.Backpack;
using DCL.Backpack.BackpackBus;
using DCL.Backpack.EmotesSection;
using DCL.Backpack.Gifting.Services.PendingTransfers;
using DCL.Browser;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.UI;
using ECS.StreamableLoading.Common.Components;
using ECS.StreamableLoading.Textures;
using NSubstitute;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using UnityEngine.Pool;
using Object = UnityEngine.Object;

namespace DCL.Tests.Editor
{
    public class BackpackEmoteGridControllerShould
    {
        private GameObject host = null!;
        private Sprite sprite = null!;
        private UniTaskCompletionSource<Sprite> firstPageArtwork = null!;
        private UniTaskCompletionSource<Sprite> secondPageArtwork = null!;
        private BackpackEventBus eventBus = null!;
        private BackpackSortDropdownView sort = null!;
        private BackpackEmoteGridController grid = null!;
        private EmotesView section = null!;
        private List<IEmote> emotes = null!;
        private IThumbnailProvider thumbnails = null!;
        private int availableBaseCount;
        private bool ownedUnavailable;
        private IEmote? ownedItem;
        private MemoryEmotesStorage storage = null!;
        private UniTaskCompletionSource? ownedGate;
        private UniTaskCompletionSource? baseGate;

        [SetUp]
        public void SetUp()
        {
            availableBaseCount = 0;
            ownedUnavailable = false;
            ownedItem = null;
            ownedGate = null;
            baseGate = null;
            eventBus = new BackpackEventBus();
            host = new GameObject();
            host.SetActive(false);
            firstPageArtwork = new UniTaskCompletionSource<Sprite>();
            secondPageArtwork = new UniTaskCompletionSource<Sprite>();
            sprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 1, 1), Vector2.zero);

            GameObject backpack = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/DCL/Backpack/Assets/Backpack.prefab"), host.transform);
            section = backpack.GetComponentInChildren<EmotesView>(true);
            var itemPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/DCL/Backpack/Assets/BackpackEmoteGridItem.prefab").GetComponent<BackpackEmoteGridItemView>();
            var pageButton = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/DCL/UI/Assets/PageButton.prefab").GetComponent<PageButtonView>();
            var pool = new ObjectPool<BackpackEmoteGridItemView>(() => Object.Instantiate(itemPrefab, section.GridView.transform));

            emotes = Enumerable.Range(1, 17).Select(index => (IEmote)new Emote(
                new StreamableLoadingResult<EmoteDTO>(new EmoteDTO
                {
                    metadata = new EmoteDTO.EmoteMetadataDto { id = $"urn:decentraland:off-chain:base-emotes:{index}", name = $"Emote {index}" },
                }), false)).ToList();
            storage = new MemoryEmotesStorage();
            storage.SetBaseEmotesUrns(emotes.Select(emote => emote.GetUrn()).ToList());

            var provider = Substitute.For<IEmoteProvider>();
            provider.GetTrimmedByParamsAsync(Arg.Any<IEmoteProvider.OwnedEmotesRequestOptions>(), Arg.Any<CancellationToken>(), Arg.Any<List<ITrimmedEmote>>())
                    .Returns(call => GetOwnedAsync(call.Arg<List<ITrimmedEmote>>()));
            provider.GetMetadataByPointersAsync(Arg.Any<IReadOnlyCollection<URN>>(), Arg.Any<CancellationToken>(), Arg.Any<List<IEmote>>())
                    .Returns(call => GetBaseAsync(call.Arg<List<IEmote>>()));

            thumbnails = Substitute.For<IThumbnailProvider>();
            thumbnails.GetAsync(Arg.Any<IThumbnailAttachment>(), Arg.Any<CancellationToken>(), Arg.Any<int>())
                      .Returns(call => call.Arg<IThumbnailAttachment>().GetUrn().Equals(emotes[16].GetUrn()) ? secondPageArtwork.Task : firstPageArtwork.Task);

            var icons = AssetDatabase.LoadAssetAtPath<NftTypeIconSO>("Assets/DCL/ScriptableObjectsConfigurations/NftRarityBackgrounds.asset");
            var colors = AssetDatabase.LoadAssetAtPath<NFTColorsSO>("Assets/DCL/ScriptableObjectsConfigurations/NftRarityColors.asset");
            sort = backpack.GetComponentInChildren<BackpackSortDropdownView>(true);
            grid = new BackpackEmoteGridController(section, new BackpackCommandBus(), eventBus, icons, colors, icons,
                Substitute.For<IEquippedEmotes>(), new BackpackSortController(sort),
                pageButton, pool, provider, thumbnails, new UnityAppWebBrowser(Substitute.For<IDecentralandUrlsSource>()), storage, Substitute.For<IPendingTransferService>());
        }

        [TearDown]
        public void TearDown()
        {
            grid.Deactivate();
            ownedGate?.TrySetCanceled();
            baseGate?.TrySetCanceled();
            grid.Dispose();
            firstPageArtwork.TrySetCanceled();
            secondPageArtwork.TrySetCanceled();
            Object.DestroyImmediate(host);
            Object.DestroyImmediate(sprite);
        }

        [Test]
        public void RecoverAnEmptyCatalogAndPageWhileThumbnailsArePending()
        {
            grid.Activate();
            Assert.That(section.CatalogRetry.gameObject.activeSelf, Is.True);
            Assert.That(section.GridView.RegularResults.activeSelf, Is.False);

            availableBaseCount = emotes.Count;
            section.CatalogRetry.onClick.Invoke();
            BackpackEmoteGridItemView[] items = section.GridView.GetComponentsInChildren<BackpackEmoteGridItemView>(true);
            Assert.That(section.CatalogRetry.gameObject.activeSelf, Is.False);
            Assert.That(items.Count(item => item.FullBackpackItem.activeSelf && item.ThumbnailName.gameObject.activeSelf), Is.EqualTo(16));
            Assert.That(section.GridView.PageSelectorView.gameObject.activeSelf, Is.True);

            section.GridView.PageSelectorView.NextPage.onClick.Invoke();
            BackpackEmoteGridItemView last = items.Single(item => item.FullBackpackItem.activeSelf);
            Assert.That(last.ThumbnailName.text, Is.EqualTo("Emote 17"));

            firstPageArtwork.TrySetResult(sprite);
            Assert.That(last.WearableThumbnail.sprite, Is.Null);
            secondPageArtwork.TrySetResult(sprite);
            Assert.That(last.WearableThumbnail.sprite, Is.EqualTo(sprite));
        }

        [Test]
        public void RetryAnOwnedOnlyRequestAndAcceptAnEmptySuccessfulCatalog()
        {
            ownedUnavailable = true;
            grid.Activate();
            sort.collectiblesOnly.onValueChanged.Invoke(true);
            Assert.That(section.CatalogRetry.gameObject.activeSelf, Is.True);

            ownedUnavailable = false;
            section.CatalogRetry.onClick.Invoke();
            Assert.That(section.CatalogRetry.gameObject.activeSelf, Is.False);
            Assert.That(section.GridView.RegularResults.activeSelf, Is.True);
            Assert.That(VisibleItems(), Is.Empty);

            ownedItem = emotes[0];
            sort.collectiblesOnly.onValueChanged.Invoke(true);
            Assert.That(VisibleItems().Single().ThumbnailName.text, Is.EqualTo("Emote 1"));
        }

        [Test]
        public void OfferRetryForAMissingSearchMatchOnlyWhileBaseMetadataIsIncomplete()
        {
            availableBaseCount = 1;
            grid.Activate();
            Assert.That(section.CatalogRetry.gameObject.activeSelf, Is.False);
            Assert.That(VisibleItems().Single().ThumbnailName.text, Is.EqualTo("Emote 1"));

            eventBus.SendFilter(null, null, "Emote 17");
            Assert.That(section.CatalogRetry.gameObject.activeSelf, Is.True);
            Assert.That(section.GridView.NoSearchResults.activeSelf, Is.False);

            availableBaseCount = emotes.Count;
            section.CatalogRetry.onClick.Invoke();
            Assert.That(section.CatalogRetry.gameObject.activeSelf, Is.False);
            Assert.That(VisibleItems().Single().ThumbnailName.text, Is.EqualTo("Emote 17"));

            eventBus.SendFilter(null, null, "No matching emote");
            Assert.That(section.CatalogRetry.gameObject.activeSelf, Is.False);
            Assert.That(section.GridView.NoSearchResults.activeSelf, Is.True);
        }

        private async UniTask<(IReadOnlyList<ITrimmedEmote>, int)> GetOwnedAsync(List<ITrimmedEmote> results)
        {
            if (ownedGate != null) await ownedGate.Task;
            if (ownedUnavailable) throw new OperationCanceledException("Owned request timed out");
            if (ownedItem != null) results.Add(ownedItem);
            return (results, results.Count);
        }

        private async UniTask GetBaseAsync(List<IEmote> results)
        {
            if (baseGate != null) await baseGate.Task;
            results.AddRange(emotes.Take(availableBaseCount));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ShowBaseCardsAndKeepTheirArtworkRequestsWhenOwnedSettlesEmpty(bool lateFailure)
        {
            availableBaseCount = emotes.Count;
            ownedUnavailable = lateFailure;
            ownedGate = new UniTaskCompletionSource();
            grid.Activate();

            var before = VisibleItems().OrderBy(item => item.Slot).ToArray();
            Assert.That(before.Select(item => item.ThumbnailName.text), Is.EqualTo(emotes.Take(16).Select(emote => emote.GetName())));
            Assert.That(section.GridView.PageSelectorView.gameObject.activeSelf, Is.True);
            ownedGate.TrySetResult();

            Assert.That(VisibleItems().OrderBy(item => item.Slot), Is.EqualTo(before));
            Assert.That(section.CatalogRetry.gameObject.activeSelf, Is.False);
            _ = thumbnails.Received(16).GetAsync(Arg.Any<IThumbnailAttachment>(), Arg.Any<CancellationToken>(), Arg.Any<int>());
        }

        [Test]
        public void ShowCachedMetadataInConfiguredOrderWhileBothProvidersArePendingAndKeepItOnPartialFailure()
        {
            storage.Set(emotes[5].GetUrn(), emotes[5]);
            storage.Set(emotes[2].GetUrn(), emotes[2]);
            ownedGate = new UniTaskCompletionSource();
            baseGate = new UniTaskCompletionSource();
            ownedUnavailable = true;
            grid.Activate();

            Assert.That(VisibleItems().OrderBy(item => item.Slot).Select(item => item.ThumbnailName.text), Is.EqualTo(new[] { "Emote 3", "Emote 6" }));
            ownedGate.TrySetResult();
            availableBaseCount = 1;
            baseGate.TrySetResult();

            Assert.That(VisibleItems().OrderBy(item => item.Slot).Select(item => item.ThumbnailName.text), Is.EqualTo(new[] { "Emote 1", "Emote 3", "Emote 6" }));
            Assert.That(section.CatalogRetry.gameObject.activeSelf, Is.False);
        }

        [Test]
        public void ShowOwnedFirstAndIgnoreArtworkFromBindingsDisplacedOnTheSamePage()
        {
            availableBaseCount = 16;
            ownedItem = emotes[16];
            ownedGate = new UniTaskCompletionSource();
            grid.Activate();

            Assert.That(VisibleItems().Single(item => item.Slot == 0).ThumbnailName.text, Is.EqualTo("Emote 1"));
            ownedGate.TrySetResult();
            var after = VisibleItems().OrderBy(item => item.Slot).ToArray();
            Assert.That(after.Select(item => item.ThumbnailName.text), Is.EqualTo(new[] { "Emote 17" }.Concat(emotes.Take(15).Select(emote => emote.GetName()))));
            firstPageArtwork.TrySetResult(sprite);
            Assert.That(after[0].WearableThumbnail.sprite, Is.Null);
            secondPageArtwork.TrySetResult(sprite);
            Assert.That(after[0].WearableThumbnail.sprite, Is.SameAs(sprite));
        }

        [Test]
        public void KeepTheSelectedSecondPageWhenItsProvisionalRowsAreFinalized()
        {
            availableBaseCount = emotes.Count;
            ownedGate = new UniTaskCompletionSource();
            grid.Activate();
            section.GridView.PageSelectorView.NextPage.onClick.Invoke();
            var last = VisibleItems().Single();
            Assert.That(last.ThumbnailName.text, Is.EqualTo("Emote 17"));

            ownedGate.TrySetResult();
            Assert.That(VisibleItems().Single(), Is.SameAs(last));
            var selected = section.GridView.PageSelectorView.GetComponentsInChildren<PageButtonView>(true)
                                  .Single(button => button.gameObject.activeSelf && button.SelectedBackground.activeSelf);
            Assert.That(selected.PageIndex, Is.EqualTo(2));
            _ = thumbnails.Received(17).GetAsync(Arg.Any<IThumbnailAttachment>(), Arg.Any<CancellationToken>(), Arg.Any<int>());
        }

        [TestCase(true)]
        [TestCase(false)]
        public async Task RetryATimedOutThumbnailOnceWhileItsNamedCardRemainsVisible(bool retrySucceeds)
        {
            availableBaseCount = 1;
            int attempts = 0;
            thumbnails.GetAsync(Arg.Any<IThumbnailAttachment>(), Arg.Any<CancellationToken>(), Arg.Any<int>())
                      .Returns(_ => ++attempts == 1 ? firstPageArtwork.Task : secondPageArtwork.Task);
            grid.Activate();
            var item = VisibleItems().Single();
            emotes[0].ThumbnailAssetResult = StreamableLoadingResult<SpriteData>.WithFallback.Failed();
            firstPageArtwork.TrySetResult(LoadThumbnailsUtils.DEFAULT_THUMBNAIL.Sprite);

            Assert.That(attempts, Is.EqualTo(1));
            Assert.That(item.ThumbnailName.text, Is.EqualTo("Emote 1"));
            Assert.That(item.ThumbnailName.gameObject.activeSelf, Is.True);
            Assert.That(item.FullBackpackItem.activeSelf, Is.True);
            await UniTask.DelayFrame(2, PlayerLoopTiming.LastPostLateUpdate);
            Assert.That(attempts, Is.EqualTo(2));
            if (retrySucceeds)
                emotes[0].ThumbnailAssetResult = new StreamableLoadingResult<SpriteData>.WithFallback(new SpriteData(new TextureData(sprite.texture), sprite));
            secondPageArtwork.TrySetResult(retrySucceeds ? sprite : LoadThumbnailsUtils.DEFAULT_THUMBNAIL.Sprite);
            await UniTask.DelayFrame(2, PlayerLoopTiming.LastPostLateUpdate);

            Assert.That(VisibleItems().Single(), Is.SameAs(item));
            Assert.That(item.WearableThumbnail.sprite, Is.EqualTo(retrySucceeds ? sprite : null));
            Assert.That(item.ThumbnailName.gameObject.activeSelf, Is.EqualTo(!retrySucceeds));
            Assert.That(attempts, Is.EqualTo(2));
        }

        [Test]
        public async Task KeepTheNameWithoutRetryWhenTheProviderSuccessfullyResolvesDefaultArtwork()
        {
            availableBaseCount = 1;
            grid.Activate();
            emotes[0].ThumbnailAssetResult = new StreamableLoadingResult<SpriteData>.WithFallback(LoadThumbnailsUtils.DEFAULT_THUMBNAIL);
            firstPageArtwork.TrySetResult(LoadThumbnailsUtils.DEFAULT_THUMBNAIL.Sprite);
            await UniTask.DelayFrame(2, PlayerLoopTiming.LastPostLateUpdate);

            var item = VisibleItems().Single();
            Assert.That(item.WearableThumbnail.sprite, Is.Null);
            Assert.That(item.ThumbnailName.gameObject.activeSelf, Is.True);
            _ = thumbnails.Received(1).GetAsync(Arg.Any<IThumbnailAttachment>(), Arg.Any<CancellationToken>(), Arg.Any<int>());
        }

        [TestCase(false)]
        [TestCase(true)]
        public async Task KeepARecycledCardIndependentOfThePreviousPagesRetry(bool retryAlreadyStarted)
        {
            availableBaseCount = emotes.Count;
            var retryArtwork = new UniTaskCompletionSource<Sprite>();
            int firstPageAttempts = 0;
            thumbnails.GetAsync(Arg.Any<IThumbnailAttachment>(), Arg.Any<CancellationToken>(), Arg.Any<int>())
                      .Returns(call =>
                       {
                           var item = call.Arg<IThumbnailAttachment>();
                           if (item.GetUrn().Equals(emotes[16].GetUrn()))
                               return secondPageArtwork.Task;
                           firstPageAttempts++;
                           return item.ThumbnailAssetResult.HasValue ? retryArtwork.Task : firstPageArtwork.Task;
                       });
            try
            {
                grid.Activate();
                var previousPage = VisibleItems().ToArray();
                foreach (IEmote emote in emotes.Take(16))
                    emote.ThumbnailAssetResult = StreamableLoadingResult<SpriteData>.WithFallback.Failed();
                firstPageArtwork.TrySetResult(LoadThumbnailsUtils.DEFAULT_THUMBNAIL.Sprite);
                if (retryAlreadyStarted)
                    await UniTask.DelayFrame(2, PlayerLoopTiming.LastPostLateUpdate);
                int expectedAttempts = retryAlreadyStarted ? 32 : 16;
                Assert.That(firstPageAttempts, Is.EqualTo(expectedAttempts));

                section.GridView.PageSelectorView.NextPage.onClick.Invoke();
                var reused = VisibleItems().Single();
                Assert.That(previousPage, Does.Contain(reused));
                retryArtwork.TrySetResult(sprite);
                await UniTask.DelayFrame(2, PlayerLoopTiming.LastPostLateUpdate);

                Assert.That(firstPageAttempts, Is.EqualTo(expectedAttempts));
                Assert.That(reused.ThumbnailName.text, Is.EqualTo("Emote 17"));
                Assert.That(reused.ThumbnailName.gameObject.activeSelf, Is.True);
                Assert.That(reused.WearableThumbnail.sprite, Is.Null);
                secondPageArtwork.TrySetResult(sprite);
                Assert.That(reused.WearableThumbnail.sprite, Is.EqualTo(sprite));
            }
            finally { retryArtwork.TrySetCanceled(); }
        }

        private IEnumerable<BackpackEmoteGridItemView> VisibleItems() =>
            section.GridView.GetComponentsInChildren<BackpackEmoteGridItemView>(true).Where(item => item.FullBackpackItem.activeSelf);
    }
}
