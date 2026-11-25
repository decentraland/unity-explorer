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
            grid = new BackpackEmoteGridController(section, new BackpackCommandBus(), eventBus, icons, colors, AssetDatabase.LoadAssetAtPath<NftTypeIconSO>("Assets/DCL/ScriptableObjectsConfigurations/NftCategoryIcons.asset"),
                new EquippedEmotes(), new BackpackSortController(sort),
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

        [UnityEngine.TestTools.UnityTest]
        public System.Collections.IEnumerator CaptureControlledRetryScreens()
        {
            const int width = 1600, height = 900;
            var root = new GameObject("QA emotes canvas", typeof(RectTransform), typeof(Canvas));
            var cameraObject = new GameObject("QA emotes camera", typeof(Camera));
            var camera = cameraObject.GetComponent<Camera>();
            var target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
            var oldTarget = RenderTexture.active;
            var previousParent = section.transform.parent;
            var oldDefaultPipeline = UnityEngine.Rendering.GraphicsSettings.defaultRenderPipeline;
            var oldQualityPipeline = QualitySettings.renderPipeline;
            try
            {
                Assert.That(SystemInfo.graphicsDeviceType, Is.Not.EqualTo(UnityEngine.Rendering.GraphicsDeviceType.Null));
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color32(50, 45, 55, 255);
                camera.transform.position = new Vector3(0, 0, -1000);
                camera.orthographic = true;
                camera.orthographicSize = height / 2f;
                camera.nearClipPlane = 1;
                camera.farClipPlane = 2000;
                root.layer = 5;
                camera.cullingMask = ~0;
                UnityEngine.Rendering.GraphicsSettings.defaultRenderPipeline = null;
                QualitySettings.renderPipeline = null;
                target.Create();
                camera.targetTexture = target;

                var canvas = root.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                canvas.planeDistance = 10f;
                section.transform.SetParent(root.transform, false);
                section.gameObject.SetActive(true);
                grid.Activate();
                Assert.That(section.CatalogRetry.gameObject.activeInHierarchy, Is.True);
                yield return null;
                yield return null;
                Capture("01-empty-retry");

                availableBaseCount = 2;
                section.CatalogRetry.onClick.Invoke();
                Assert.That(VisibleItems().Count(), Is.EqualTo(2));
                Assert.That(section.CatalogRetry.gameObject.activeSelf, Is.False);
                yield return null;
                yield return null;
                Capture("02-partial-names");

                eventBus.SendFilter(null, null, "Emote 17");
                Assert.That(section.CatalogRetry.gameObject.activeInHierarchy, Is.True);
                Assert.That(section.GridView.NoSearchResults.activeInHierarchy, Is.False);
                yield return null;
                yield return null;
                Capture("03-incomplete-search-retry");

                availableBaseCount = emotes.Count;
                section.CatalogRetry.onClick.Invoke();
                Assert.That(VisibleItems().Single().ThumbnailName.text, Is.EqualTo("Emote 17"));
                yield return null;
                yield return null;
                Capture("04-recovered-match");

                eventBus.SendFilter(null, null, null);
                Assert.That(VisibleItems().Count(), Is.EqualTo(16));
                Assert.That(VisibleItems().All(item => item.ThumbnailName.gameObject.activeInHierarchy && !item.WearableThumbnail.enabled), Is.True);
                yield return null;
                yield return null;
                Capture("05-recovered-first-page");
                var files = System.IO.Directory.GetFiles(System.IO.Path.Combine(Application.dataPath, "../../native-evidence"), "*.png");
                var lengths = files.Select(file => new System.IO.FileInfo(file).Length).Distinct().Count();
                Assert.That(lengths, Is.GreaterThanOrEqualTo(3), "State changes must alter the rendered pixels.");

                void Capture(string state)
                {
                    Canvas.ForceUpdateCanvases();
                    UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)section.transform);
                    Canvas.ForceUpdateCanvases();
                    foreach (var label in section.GetComponentsInChildren<TMPro.TMP_Text>())
                        label.ForceMeshUpdate();
                    camera.Render();
                    RenderTexture.active = target;
                    var frame = new Texture2D(width, height, TextureFormat.RGB24, false);
                    try
                    {
                        frame.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                        frame.Apply();
                        var folder = System.IO.Path.Combine(Application.dataPath, "../../native-evidence");
                        System.IO.Directory.CreateDirectory(folder);
                        System.IO.File.WriteAllBytes(System.IO.Path.Combine(folder, state + ".png"), frame.EncodeToPNG());
                        var visible = VisibleItems().Where(item => item.ThumbnailName.gameObject.activeInHierarchy).Select(item => item.ThumbnailName.text);
                        var rect = (RectTransform)section.CatalogRetry.transform;
                        var corners = new Vector3[4];
                        rect.GetWorldCorners(corners);
                        var lower = camera.WorldToScreenPoint(corners[0]);
                        var upper = camera.WorldToScreenPoint(corners[2]);
                        var retryLabel = section.CatalogRetry.GetComponent<TMPro.TMP_Text>();
                        var pixels = frame.GetPixels32();
                        int changed = pixels.Count(p => !p.Equals(pixels[0]));
                        System.IO.File.AppendAllText(System.IO.Path.Combine(folder, "states.txt"),
                            $"{state}\tunity={Application.unityVersion}\tdevice={SystemInfo.graphicsDeviceName}\tretry={section.CatalogRetry.gameObject.activeInHierarchy}\tlabels={string.Join(",", visible)}\tchanged={changed}\tretryRect={lower}/{upper}\tmesh={retryLabel.textInfo.characterCount}\tcanvasRect={canvas.pixelRect}\n");
                    }
                    finally { Object.DestroyImmediate(frame); }
                }
            }
            finally
            {
                UnityEngine.Rendering.GraphicsSettings.defaultRenderPipeline = oldDefaultPipeline;
                QualitySettings.renderPipeline = oldQualityPipeline;
                section.transform.SetParent(previousParent, false);
                camera.targetTexture = null;
                RenderTexture.active = oldTarget;
                target.Release();
                Object.DestroyImmediate(target);
                Object.DestroyImmediate(cameraObject);
                Object.DestroyImmediate(root);
            }
        }

        private IEnumerable<BackpackEmoteGridItemView> VisibleItems() =>
            section.GridView.GetComponentsInChildren<BackpackEmoteGridItemView>(true).Where(item => item.FullBackpackItem.activeSelf);
    }
}



