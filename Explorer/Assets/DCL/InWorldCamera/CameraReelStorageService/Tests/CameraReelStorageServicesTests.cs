using DCL.Browser.DecentralandUrls;
using DCL.InWorldCamera.CameraReelStorageService.Schemas;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.WebRequests;
using NUnit.Framework;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace DCL.InWorldCamera.CameraReelStorageService.Tests
{
    public class CameraReelStorageServicesTests
    {
        private const DecentralandEnvironment ENVIRONMENT = DecentralandEnvironment.Zone;

        private const string ADDRESS = "0x05dE05303EAb867D51854E8b4fE03F7acb0624d9";
        private const int IMAGE_WIDTH = 1920;
        private const int IMAGE_HEIGHT = 1080;

        private readonly CancellationToken ct = CancellationToken.None;
        private readonly IWebRequestController webRequestController = IWebRequestController.DEFAULT;

        private ICameraReelImagesStorage imagesStorage;
        private ICameraReelImagesMetadataDatabase metadataDatabase;

        [SetUp]
        public void SetUp()
        {
            var urlsSource = new DecentralandUrlsSource(ENVIRONMENT);

            metadataDatabase = new CameraReelImagesMetadataRemoteDatabase(webRequestController, urlsSource);
            imagesStorage = new CameraReelS3BucketImagesStorage(webRequestController);
        }

        [Test]
        public async Task GetStorage_ShouldReturnValidStorageInfo()
        {
            // Act
            CameraReelStorageResponse storage = await metadataDatabase.GetStorageInfo(ADDRESS, ct);

            // Assert
            Assert.That(storage, Is.Not.Null);
            Assert.That(storage.maxImages, Is.GreaterThan(0));
            Assert.That(storage.currentImages, Is.GreaterThan(0));
        }

        [Test]
        public async Task GetGallery_ShouldReturnValidMetadataArray()
        {
            // Arrange
            const int LIMIT = 10;

            // Act
            CameraReelResponses result = await metadataDatabase.GetScreenshots(ADDRESS, LIMIT, offset: 0, ct);

            // Assert
            Assert.That(result, Is.Not.Null);
            Assert.That(result.images.Count, Is.EqualTo(LIMIT));
            Assert.That(result.maxImages, Is.GreaterThan(0));
            Assert.That(result.currentImages, Is.GreaterThan(0));
        }

        [Test]
        public async Task GetGallery_ShouldReturnValidScreenshots()
        {
            // Arrange
            const int LIMIT = 1;

            // Act
            CameraReelResponses result = await metadataDatabase.GetScreenshots(ADDRESS, LIMIT, offset: 0, ct);

            // Assert
            Assert.That(result.images.Count, Is.EqualTo(LIMIT));

            if (result.images.Any())
            {
                CameraReelResponse? firstScreenshot = result.images.First();
                Assert.That(firstScreenshot.url, Is.Not.Empty);
                Assert.That(firstScreenshot.thumbnailUrl, Is.Not.Empty);

                // Test image retrieval
                Texture2D image = await imagesStorage.GetScreenshotImage(firstScreenshot.url);
                Assert.That(image, Is.Not.Null);
                Assert.That(image.width, Is.EqualTo(IMAGE_WIDTH));
                Assert.That(image.height, Is.EqualTo(IMAGE_HEIGHT));

                // Test thumbnail retrieval
                Texture2D thumbnail = await imagesStorage.GetScreenshotThumbnail(firstScreenshot.thumbnailUrl);
                Assert.That(thumbnail, Is.Not.Null);
                Assert.That(thumbnail.width, Is.GreaterThan(0));
                Assert.That(thumbnail.height, Is.GreaterThan(0));
            }
        }
    }
}
