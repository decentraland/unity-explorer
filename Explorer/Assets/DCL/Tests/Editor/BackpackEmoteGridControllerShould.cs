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
        [Test]
        public void RecoverAnEmptyCatalogAndPageWhileThumbnailsArePending()
        {
            var host = new GameObject();
            host.SetActive(false);
            var firstPageArtwork = new UniTaskCompletionSource<Sprite>();
            var secondPageArtwork = new UniTaskCompletionSource<Sprite>();
            Sprite sprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 1, 1), Vector2.zero);

            try
            {
                GameObject backpack = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/DCL/Backpack/Assets/Backpack.prefab"), host.transform);
                EmotesView section = backpack.GetComponentInChildren<EmotesView>(true);
                var itemPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/DCL/Backpack/Assets/BackpackEmoteGridItem.prefab").GetComponent<BackpackEmoteGridItemView>();
                var pageButton = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/DCL/UI/Assets/PageButton.prefab").GetComponent<PageButtonView>();
                var pool = new ObjectPool<BackpackEmoteGridItemView>(() => Object.Instantiate(itemPrefab, section.GridView.transform));

                List<IEmote> emotes = Enumerable.Range(1, 17).Select(index => (IEmote)new Emote(
                    new StreamableLoadingResult<EmoteDTO>(new EmoteDTO
                    {
                        metadata = new EmoteDTO.EmoteMetadataDto { id = $"urn:decentraland:off-chain:base-emotes:{index}", name = $"Emote {index}" },
                    }), false)).ToList();
                var storage = new MemoryEmotesStorage();
                storage.SetBaseEmotesUrns(emotes.Select(emote => emote.GetUrn()).ToList());

                IEmoteProvider provider = Substitute.For<IEmoteProvider>();
                provider.GetTrimmedByParamsAsync(Arg.Any<IEmoteProvider.OwnedEmotesRequestOptions>(), Arg.Any<CancellationToken>(), Arg.Any<List<ITrimmedEmote>>())
                        .Returns(UniTask.FromResult<(IReadOnlyList<ITrimmedEmote>, int)>((Array.Empty<ITrimmedEmote>(), 0)));
                bool metadataAvailable = false;
                provider.GetMetadataByPointersAsync(Arg.Any<IReadOnlyCollection<URN>>(), Arg.Any<CancellationToken>(), Arg.Any<List<IEmote>>())
                        .Returns(call =>
                        {
                            if (metadataAvailable)
                                call.Arg<List<IEmote>>().AddRange(emotes);

                            return UniTask.CompletedTask;
                        });

                IThumbnailProvider thumbnails = Substitute.For<IThumbnailProvider>();
                thumbnails.GetAsync(Arg.Any<IThumbnailAttachment>(), Arg.Any<CancellationToken>(), Arg.Any<int>())
                          .Returns(call => call.Arg<IThumbnailAttachment>().GetUrn().Equals(emotes[16].GetUrn()) ? secondPageArtwork.Task : firstPageArtwork.Task);

                var icons = AssetDatabase.LoadAssetAtPath<NftTypeIconSO>("Assets/DCL/ScriptableObjectsConfigurations/NftRarityBackgrounds.asset");
                var colors = AssetDatabase.LoadAssetAtPath<NFTColorsSO>("Assets/DCL/ScriptableObjectsConfigurations/NftRarityColors.asset");
                using var grid = new BackpackEmoteGridController(section, new BackpackCommandBus(), new BackpackEventBus(), icons, colors, icons,
                    Substitute.For<IEquippedEmotes>(), new BackpackSortController(backpack.GetComponentInChildren<BackpackSortDropdownView>(true)),
                    pageButton, pool, provider, thumbnails, new UnityAppWebBrowser(Substitute.For<IDecentralandUrlsSource>()), storage, Substitute.For<IPendingTransferService>());

                grid.Activate();
                Assert.That(section.CatalogRetry.gameObject.activeSelf, Is.True);
                Assert.That(section.GridView.RegularResults.activeSelf, Is.False);

                metadataAvailable = true;
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
                grid.Deactivate();
            }
            finally
            {
                firstPageArtwork.TrySetCanceled();
                secondPageArtwork.TrySetCanceled();
                Object.DestroyImmediate(host);
                Object.DestroyImmediate(sprite);
            }
        }
    }
}
