using Cysharp.Threading.Tasks;
using DCL.InWorldCamera.CameraReelGallery.Components;
using DCL.InWorldCamera.CameraReelStorageService.Schemas;
using DCL.UI;
using DCL.WebRequests;
using NSubstitute;
using NUnit.Framework;
using System.Threading;
using UnityEditor;
using UnityEngine;

namespace DCL.InWorldCamera.CameraReelGallery.Tests
{
    [TestFixture]
    public class ReelThumbnailControllerShould
    {
        private const string PREFAB_PATH = "Assets/DCL/InWorldCamera/CameraReelGallery/Assets/ThumbnailElement.prefab";
        private const string URL = "https://example.com/thumbnail.png";
        private const int INDEX = 3;

        private static readonly Vector2 CELL = new (306f, 230f);

        private GameObject viewGameObject = null!;
        private ReelThumbnailView view = null!;
        private ReelThumbnailController controller = null!;
        private ISpriteCache cache = null!;
        private Texture2D texture = null!;
        private Sprite sprite = null!;
        private CameraReelResponseCompact reel = null!;
        private int clickedIndex;

        [SetUp]
        public void SetUp()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PREFAB_PATH);
            Assert.IsNotNull(prefab, $"Could not load the thumbnail prefab from {PREFAB_PATH}");

            viewGameObject = Object.Instantiate(prefab);
            view = viewGameObject.GetComponent<ReelThumbnailView>();
            Assert.IsNotNull(view, "ReelThumbnailView component not found on the instantiated prefab");

            cache = Substitute.For<ISpriteCache>();
            texture = new Texture2D(192, 108);
            sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f));
            reel = new CameraReelResponseCompact { id = "reel", thumbnailUrl = URL, dateTime = "0" };
            clickedIndex = -1;

            view.Resize(CELL);
            controller = new ReelThumbnailController(view, null, index => clickedIndex = index);
        }

        [TearDown]
        public void TearDown()
        {
            controller.Dispose();
            Object.DestroyImmediate(viewGameObject);
            Object.DestroyImmediate(sprite);
            Object.DestroyImmediate(texture);
        }

        [Test]
        public void ShowACachedSpriteWithoutRequestingIt()
        {
            // Arrange
            cache.GetCachedSprite(URL).Returns(sprite);

            // Act
            controller.Bind(reel, INDEX, cache);

            // Assert
            Assert.AreSame(texture, view.thumbnailImage.texture);
            Assert.IsTrue(view.thumbnailImage.gameObject.activeSelf);
            Assert.IsFalse(view.loadingBrightView.gameObject.activeSelf);
            cache.DidNotReceive().GetSpriteAsync(Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<RetryPolicy?>(), Arg.Any<CancellationToken>());
        }

        [Test]
        public void RequestAnUncachedSpriteWithKtxAndShowItOnceLoaded()
        {
            // Arrange
            var download = new UniTaskCompletionSource<Sprite?>();
            cache.GetSpriteAsync(URL, true, null, Arg.Any<CancellationToken>()).Returns(download.Task);

            // Act
            controller.Bind(reel, INDEX, cache);

            // Assert
            Assert.IsNull(view.thumbnailImage.texture);
            Assert.IsTrue(view.loadingBrightView.gameObject.activeSelf);

            download.TrySetResult(sprite);

            Assert.AreSame(texture, view.thumbnailImage.texture);
            Assert.IsTrue(view.thumbnailImage.gameObject.activeSelf);
            Assert.IsFalse(view.loadingBrightView.gameObject.activeSelf);
        }

        [Test]
        public void CancelThePendingLoadWhenUnbound()
        {
            // Arrange
            var download = new UniTaskCompletionSource<Sprite?>();
            CancellationToken token = default;

            cache.GetSpriteAsync(URL, true, null, Arg.Do<CancellationToken>(ct => token = ct)).Returns(download.Task);
            controller.Bind(reel, INDEX, cache);

            // Act
            controller.Unbind();
            download.TrySetResult(sprite);

            // Assert
            Assert.IsTrue(token.IsCancellationRequested);
            Assert.IsNull(view.thumbnailImage.texture);
            Assert.IsNull(controller.Reel);
        }

        [Test]
        public void IgnoreClicksUntilTheImageIsLoaded()
        {
            // Arrange
            var download = new UniTaskCompletionSource<Sprite?>();
            cache.GetSpriteAsync(URL, true, null, Arg.Any<CancellationToken>()).Returns(download.Task);
            controller.Bind(reel, INDEX, cache);

            // Act
            view.button.onClick.Invoke();
            int indexBeforeLoad = clickedIndex;
            download.TrySetResult(sprite);
            view.button.onClick.Invoke();

            // Assert
            Assert.AreEqual(-1, indexBeforeLoad);
            Assert.AreEqual(INDEX, clickedIndex);
        }

        [Test]
        public void CentreCropTheTextureToTheCellAspect()
        {
            // Arrange
            cache.GetCachedSprite(URL).Returns(sprite);
            float ratio = texture.height / CELL.y;
            float shownWidth = ratio * CELL.x;
            float cropped = texture.width - shownWidth;

            // Act
            controller.Bind(reel, INDEX, cache);

            // Assert
            Rect uv = view.thumbnailImage.uvRect;
            Assert.AreEqual(cropped / 2f / texture.width, uv.x, 0.0001f);
            Assert.AreEqual(shownWidth / texture.width, uv.width, 0.0001f);
            Assert.AreEqual(0f, uv.y);
            Assert.AreEqual(1f, uv.height);
        }

        [Test]
        public void NotRequestAgainWhenReboundWithTheSameReel()
        {
            // Arrange
            var download = new UniTaskCompletionSource<Sprite?>();
            cache.GetSpriteAsync(URL, true, null, Arg.Any<CancellationToken>()).Returns(download.Task);
            controller.Bind(reel, INDEX, cache);

            // Act
            controller.Bind(reel, INDEX + 1, cache);
            download.TrySetResult(sprite);
            view.button.onClick.Invoke();

            // Assert
            cache.Received(1).GetSpriteAsync(URL, true, null, Arg.Any<CancellationToken>());
            Assert.AreEqual(INDEX + 1, clickedIndex);
        }

        [Test]
        public void StopListeningToTheViewOnceDisposed()
        {
            // Arrange
            cache.GetCachedSprite(URL).Returns(sprite);
            controller.Bind(reel, INDEX, cache);

            // Act
            controller.Dispose();
            view.button.onClick.Invoke();

            // Assert
            Assert.AreEqual(-1, clickedIndex);
            Assert.IsNull(controller.Reel);
            Assert.IsNull(view.thumbnailImage.texture);
        }
    }
}
