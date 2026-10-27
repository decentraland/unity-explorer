using Cysharp.Threading.Tasks;
using DCL.InWorldCamera.CameraReelGallery.Components;
using DCL.InWorldCamera.CameraReelStorageService;
using DCL.InWorldCamera.CameraReelStorageService.Schemas;
using DCL.UI;
using NSubstitute;
using NUnit.Framework;
using SuperScrollView;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace DCL.InWorldCamera.CameraReelGallery.Tests
{
    [TestFixture]
    public class CameraReelGalleryControllerShould
    {
        private const string PREFAB_PATH = "Assets/DCL/InWorldCamera/CameraReelGallery/Assets/CameraReel.prefab";
        private const string WALLET = "0x1234";
        private const int COLUMNS = 6;
        private const int COMPACT_COLUMNS = 5;
        private const int MAX_SCREENSHOTS = 500;
        private const float VIEWPORT_HEIGHT = 1080f;
        private const float TALL_VIEWPORT_HEIGHT = 20000f;

        private static readonly DateTime MARCH = new (2025, 3, 15, 12, 0, 0, DateTimeKind.Local);
        private static readonly DateTime FEBRUARY = new (2025, 2, 10, 12, 0, 0, DateTimeKind.Local);
        private static readonly Vector2 AUTHORED_CELL = new (306f, 230f);
        private static readonly Vector2 COMPACT_CELL = new (292f, 219f);

        private GameObject rootGameObject = null!;
        private RectTransform rootTransform = null!;
        private CameraReelGalleryView view = null!;
        private ICameraReelStorageService storage = null!;
        private ISpriteCache cache = null!;
        private GalleryEventBus bus = null!;
        private CameraReelGalleryController controller = null!;
        private Texture2D texture = null!;
        private Sprite sprite = null!;
        private List<CameraReelResponseCompact> serverReels = null!;

        [SetUp]
        public void SetUp()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PREFAB_PATH);
            Assert.IsNotNull(prefab, $"Could not load the camera reel prefab from {PREFAB_PATH}");

            rootGameObject = Object.Instantiate(prefab);
            rootTransform = (RectTransform)rootGameObject.transform;
            rootTransform.sizeDelta = new Vector2(1920f, VIEWPORT_HEIGHT);
            view = rootGameObject.GetComponentInChildren<CameraReelGalleryView>(true);
            Assert.IsNotNull(view, "CameraReelGalleryView not found in the instantiated prefab");

            storage = Substitute.For<ICameraReelStorageService>();
            cache = Substitute.For<ISpriteCache>();
            bus = new GalleryEventBus();
            texture = new Texture2D(4, 2);
            sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f));
            serverReels = new List<CameraReelResponseCompact>();

            storage.GetCompactScreenshotGalleryAsync(WALLET, Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
                   .Returns(call => UniTask.FromResult(Page(call.ArgAt<int>(1), call.ArgAt<int>(2))));

            controller = new CameraReelGalleryController(view, storage, cache,
                new ReelGalleryConfigParams(COLUMNS, (int)AUTHORED_CELL.y, (int)AUTHORED_CELL.x, gridShowMonth: true, groupByMonth: true), useSignedRequest: true, bus);
        }

        [TearDown]
        public void TearDown()
        {
            controller.Dispose();
            Object.DestroyImmediate(rootGameObject);
            Object.DestroyImmediate(sprite);
            Object.DestroyImmediate(texture);
        }

        [Test]
        public async Task BindPaddingHeaderAndThumbnailRowsForTheFirstPage()
        {
            // Arrange
            AddReels(MARCH, 7);
            AddReels(FEBRUARY, 2);

            // Act
            await controller.ShowWalletGalleryAsync(WALLET, CancellationToken.None, new CameraReelStorageStatus(serverReels.Count, MAX_SCREENSHOTS));

            // Assert
            List<LoopListViewItem2> items = view.loopList.ItemList;
            Assert.AreEqual(7, view.loopList.ItemTotalCount);
            Assert.GreaterOrEqual(items.Count, 4);
            Assert.IsNull(items[0].GetComponent<ReelGalleryRowView>());
            Assert.IsNull(items[0].GetComponent<ReelGalleryMonthHeaderView>());
            Assert.AreEqual("March 2025", items[1].GetComponent<ReelGalleryMonthHeaderView>().monthText.text);
            Assert.AreEqual(COLUMNS, ActiveSlots(items[2]));
            Assert.AreEqual(1, ActiveSlots(items[3]));
            Assert.IsFalse(view.loadingSpinner.activeSelf);
            Assert.IsFalse(view.emptyState.activeSelf);
        }

        [Test]
        public async Task LayOutTheRowsWithTheCompactGridOnceSet()
        {
            // Arrange
            AddReels(MARCH, 7);
            controller.SetGridLayout(COMPACT_COLUMNS, (int)COMPACT_CELL.x, (int)COMPACT_CELL.y);

            // Act
            await controller.ShowWalletGalleryAsync(WALLET, CancellationToken.None, new CameraReelStorageStatus(serverReels.Count, MAX_SCREENSHOTS));

            // Assert
            List<LoopListViewItem2> items = view.loopList.ItemList;
            Assert.AreEqual(COMPACT_COLUMNS, ActiveSlots(items[2]));
            Assert.AreEqual(2, ActiveSlots(items[3]));
            Assert.AreEqual(COMPACT_CELL, ((RectTransform)FirstRowSlot(0).transform).sizeDelta);
        }

        [Test]
        public async Task RelayOutThePooledRowsWhenTheAuthoredGridIsRestored()
        {
            // Arrange
            AddReels(MARCH, 7);
            controller.SetGridLayout(COMPACT_COLUMNS, (int)COMPACT_CELL.x, (int)COMPACT_CELL.y);
            await controller.ShowWalletGalleryAsync(WALLET, CancellationToken.None, new CameraReelStorageStatus(serverReels.Count, MAX_SCREENSHOTS));
            controller.RestoreGridLayout();

            // Act
            await controller.ShowWalletGalleryAsync(WALLET, CancellationToken.None, new CameraReelStorageStatus(serverReels.Count, MAX_SCREENSHOTS));

            // Assert
            List<LoopListViewItem2> items = view.loopList.ItemList;
            Assert.AreEqual(COLUMNS, ActiveSlots(items[2]));
            Assert.AreEqual(1, ActiveSlots(items[3]));
            Assert.AreEqual(AUTHORED_CELL, ((RectTransform)FirstRowSlot(0).transform).sizeDelta);
        }

        [Test]
        public async Task RaiseMaxThumbnailsUpdatedWithTheLoadedCount()
        {
            // Arrange
            AddReels(MARCH, 9);
            int reported = -1;
            controller.MaxThumbnailsUpdated += count => reported = count;

            // Act
            await controller.ShowWalletGalleryAsync(WALLET, CancellationToken.None, new CameraReelStorageStatus(serverReels.Count, MAX_SCREENSHOTS));

            // Assert
            Assert.AreEqual(9, reported);
        }

        [Test]
        public async Task ShowTheEmptyStateWithoutRequestingPagesWhenThereAreNoScreenshots()
        {
            // Act
            await controller.ShowWalletGalleryAsync(WALLET, CancellationToken.None, new CameraReelStorageStatus(0, MAX_SCREENSHOTS));

            // Assert
            Assert.IsTrue(view.emptyState.activeSelf);
            Assert.IsFalse(view.loadingSpinner.activeSelf);
            await storage.DidNotReceive().GetCompactScreenshotGalleryAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>());
        }

        [Test]
        public async Task InvokeThumbnailClickedWithTheOrderedListAndFlatIndex()
        {
            // Arrange
            AddReels(MARCH, 9);
            cache.GetCachedSprite(Arg.Any<string>()).Returns(sprite);
            List<CameraReelResponseCompact>? clickedReels = null;
            int clickedIndex = -1;
            controller.ThumbnailClicked += (reels, index, _, _) => { clickedReels = reels; clickedIndex = index; };
            await controller.ShowWalletGalleryAsync(WALLET, CancellationToken.None, new CameraReelStorageStatus(serverReels.Count, MAX_SCREENSHOTS));

            // Act
            SecondRowSlot(0).button.onClick.Invoke();

            // Assert
            Assert.AreEqual(COLUMNS, clickedIndex);
            Assert.IsNotNull(clickedReels);
            Assert.AreEqual(serverReels[COLUMNS].id, clickedReels![COLUMNS].id);
        }

        [Test]
        public async Task RemoveTheReelAndRebindRowsOnADeletionSignal()
        {
            // Arrange
            AddReels(MARCH, 9);
            cache.GetCachedSprite(Arg.Any<string>()).Returns(sprite);
            await controller.ShowWalletGalleryAsync(WALLET, CancellationToken.None, new CameraReelStorageStatus(serverReels.Count, MAX_SCREENSHOTS));
            string removedId = serverReels[0].id;

            // Act
            bus.ReelDeleted(removedId);

            // Assert
            Assert.AreEqual(serverReels[1].id, FirstRowSlot(0).Reel!.id);
            Assert.AreEqual(2, ActiveSlots(view.loopList.ItemList[3]));
            Assert.IsFalse(view.emptyState.activeSelf);
        }

        [Test]
        public async Task IgnoreADeletionSignalForAnUnknownReel()
        {
            // Arrange
            AddReels(MARCH, 9);
            cache.GetCachedSprite(Arg.Any<string>()).Returns(sprite);
            await controller.ShowWalletGalleryAsync(WALLET, CancellationToken.None, new CameraReelStorageStatus(serverReels.Count, MAX_SCREENSHOTS));

            // Act
            bus.ReelDeleted("unknown");

            // Assert
            Assert.AreEqual(serverReels[0].id, FirstRowSlot(0).Reel!.id);
            Assert.AreEqual(3, ActiveSlots(view.loopList.ItemList[3]));
        }

        [Test]
        public async Task ShowTheEmptyStateWhenTheLastReelIsDeleted()
        {
            // Arrange
            AddReels(MARCH, 1);
            cache.GetCachedSprite(Arg.Any<string>()).Returns(sprite);
            await controller.ShowWalletGalleryAsync(WALLET, CancellationToken.None, new CameraReelStorageStatus(serverReels.Count, MAX_SCREENSHOTS));

            // Act
            bus.ReelDeleted(serverReels[0].id);

            // Assert
            Assert.IsTrue(view.emptyState.activeSelf);
        }

        [Test]
        public async Task RequestOnlyTheFirstPageWhileTheBoundRowsAreClearOfTheThreshold()
        {
            // Arrange
            AddReels(MARCH, view.PaginationLimit * 3);

            // Act
            await controller.ShowWalletGalleryAsync(WALLET, CancellationToken.None, new CameraReelStorageStatus(serverReels.Count, MAX_SCREENSHOTS));

            // Assert
            await storage.Received(1).GetCompactScreenshotGalleryAsync(WALLET, view.PaginationLimit, 0, Arg.Any<CancellationToken>());
            await storage.DidNotReceive().GetCompactScreenshotGalleryAsync(WALLET, view.PaginationLimit, view.PaginationLimit, Arg.Any<CancellationToken>());
        }

        [Test]
        public async Task ChainPagesAsTheShownRowsReachTheThreshold()
        {
            // Arrange: a viewport taller than a page shows every row it has, and each page completes only on demand.
            rootTransform.sizeDelta = new Vector2(1920f, TALL_VIEWPORT_HEIGHT);
            view.loopList.ResetListView();
            int pageSize = view.PaginationLimit;
            AddReels(MARCH, (pageSize * 2) + (pageSize / 2));
            var pendingPages = new Dictionary<int, UniTaskCompletionSource<CameraReelResponsesCompact>>();

            storage.GetCompactScreenshotGalleryAsync(WALLET, pageSize, Arg.Any<int>(), Arg.Any<CancellationToken>())
                   .Returns(call =>
                    {
                        var page = new UniTaskCompletionSource<CameraReelResponsesCompact>();
                        pendingPages[call.ArgAt<int>(2)] = page;
                        return page.Task;
                    });

            // Act
            UniTask show = controller.ShowWalletGalleryAsync(WALLET, CancellationToken.None, new CameraReelStorageStatus(serverReels.Count, MAX_SCREENSHOTS));
            pendingPages[0].TrySetResult(Page(pageSize, 0));
            bool secondPageRequested = pendingPages.ContainsKey(pageSize);
            pendingPages[pageSize].TrySetResult(Page(pageSize, pageSize));
            bool thirdPageRequestedBeforeFill = pendingPages.ContainsKey(pageSize * 2);
            FillList();
            bool thirdPageRequestedAfterFill = pendingPages.ContainsKey(pageSize * 2);
            pendingPages[pageSize * 2].TrySetResult(Page(pageSize, pageSize * 2));
            await show;

            // Assert
            Assert.IsTrue(secondPageRequested);
            Assert.IsFalse(thirdPageRequestedBeforeFill);
            Assert.IsTrue(thirdPageRequestedAfterFill);
            Assert.IsFalse(pendingPages.ContainsKey(pageSize * 3));
        }

        [Test]
        public async Task ClearRowsAndTheThumbnailCacheWhenDisposed()
        {
            // Arrange
            AddReels(MARCH, 9);
            await controller.ShowWalletGalleryAsync(WALLET, CancellationToken.None, new CameraReelStorageStatus(serverReels.Count, MAX_SCREENSHOTS));

            // Act
            controller.Dispose();

            // Assert
            Assert.AreEqual(0, view.loopList.ItemTotalCount);
            Assert.AreEqual(0, view.loopList.ItemList.Count);
            cache.Received(1).Clear();
        }

        private void AddReels(DateTime month, int count)
        {
            for (var i = 0; i < count; i++)
                serverReels.Add(new CameraReelResponseCompact
                {
                    id = $"reel-{serverReels.Count}",
                    thumbnailUrl = $"https://example.com/{serverReels.Count}.png",
                    dateTime = new DateTimeOffset(month).ToUnixTimeSeconds().ToString(),
                });
        }

        private CameraReelResponsesCompact Page(int limit, int offset)
        {
            var page = new CameraReelResponsesCompact { currentImages = serverReels.Count, maxImages = MAX_SCREENSHOTS };

            for (int i = offset; i < serverReels.Count && i < offset + limit; i++)
                page.images.Add(serverReels[i]);

            return page;
        }

        private static int ActiveSlots(LoopListViewItem2 item)
        {
            IReadOnlyList<ReelThumbnailView> slots = item.GetComponent<ReelGalleryRowView>().slots;
            var active = 0;

            for (var i = 0; i < slots.Count; i++)
                if (slots[i].gameObject.activeSelf)
                    active++;

            return active;
        }

        // Shows the rows that fit the viewport, which the list otherwise does in Update.
        private void FillList()
        {
            LoopListViewInitParam defaults = LoopListViewInitParam.CopyDefaultInitParam();
            view.loopList.UpdateListView(defaults.mDistanceForRecycle0, defaults.mDistanceForRecycle1, defaults.mDistanceForNew0, defaults.mDistanceForNew1);
        }

        private ReelThumbnailView FirstRowSlot(int slot) =>
            view.loopList.ItemList[2].GetComponent<ReelGalleryRowView>().slots[slot];

        private ReelThumbnailView SecondRowSlot(int slot) =>
            view.loopList.ItemList[3].GetComponent<ReelGalleryRowView>().slots[slot];
    }
}
