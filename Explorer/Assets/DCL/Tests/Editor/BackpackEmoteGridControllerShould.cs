using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.AvatarRendering.Emotes;
using DCL.AvatarRendering.Emotes.Equipped;
using DCL.AvatarRendering.Loading.Components;
using DCL.AvatarRendering.Wearables;
using DCL.Backpack;
using DCL.Backpack.BackpackBus;
using DCL.Backpack.EmotesSection;
using DCL.Backpack.Gifting.Services.PendingTransfers;
using DCL.Browser;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.UI;
using ECS.StreamableLoading.Common.Components;
using NSubstitute;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
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
        private int availableBaseCount;
        private bool ownedUnavailable;
        private bool ownedItemAvailable;

        [SetUp]
        public void SetUp()
        {
            availableBaseCount = 0;
            ownedUnavailable = false;
            ownedItemAvailable = false;
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
            var storage = new MemoryEmotesStorage();
            storage.SetBaseEmotesUrns(emotes.Select(emote => emote.GetUrn()).ToList());

            var provider = Substitute.For<IEmoteProvider>();
            provider.GetTrimmedByParamsAsync(Arg.Any<IEmoteProvider.OwnedEmotesRequestOptions>(), Arg.Any<CancellationToken>(), Arg.Any<List<ITrimmedEmote>>())
                    .Returns(call =>
                    {
                        if (ownedUnavailable)
                            return UniTask.FromException<(IReadOnlyList<ITrimmedEmote>, int)>(new OperationCanceledException("Owned request timed out"));

                        var results = call.Arg<List<ITrimmedEmote>>();
                        if (ownedItemAvailable)
                            results.Add(emotes[0]);
                        return UniTask.FromResult<(IReadOnlyList<ITrimmedEmote>, int)>((results, results.Count));
                    });
            provider.GetMetadataByPointersAsync(Arg.Any<IReadOnlyCollection<URN>>(), Arg.Any<CancellationToken>(), Arg.Any<List<IEmote>>())
                    .Returns(call =>
                    {
                        call.Arg<List<IEmote>>().AddRange(emotes.Take(availableBaseCount));

                        return UniTask.CompletedTask;
                    });

            IThumbnailProvider thumbnails = Substitute.For<IThumbnailProvider>();
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

            ownedItemAvailable = true;
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

        private IEnumerable<BackpackEmoteGridItemView> VisibleItems() =>
            section.GridView.GetComponentsInChildren<BackpackEmoteGridItemView>(true).Where(item => item.FullBackpackItem.activeSelf);
    }
}
