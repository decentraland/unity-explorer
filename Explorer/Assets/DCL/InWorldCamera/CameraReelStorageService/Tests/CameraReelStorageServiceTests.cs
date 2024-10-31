using Cysharp.Threading.Tasks;
using DCL.InWorldCamera.CameraReelStorageService.Schemas;
using NSubstitute;
using NUnit.Framework;
using System.Threading;
using System.Threading.Tasks;

namespace DCL.InWorldCamera.CameraReelStorageService.Tests
{
    public class CameraReelStorageServiceTests
    {
        private const string USER_ADDRESS = "testAddress";
        private const int LIMIT = 10;
        private const int OFFSET = 0;

        private ICameraReelImagesMetadataDatabase metadataDatabase;
        private CameraReelRemoteStorageService storageService;

        [SetUp]
        public void Setup()
        {
            metadataDatabase = Substitute.For<ICameraReelImagesMetadataDatabase>();
            storageService = new CameraReelRemoteStorageService(metadataDatabase);
        }

        [Test]
        public async Task GetUserGalleryStorageInfo_ShouldReturnCorrectStatus()
        {
            // Arrange
            var expectedResponse = new CameraReelStorageResponse { currentImages = 5, maxImages = 10 };
            metadataDatabase.GetStorageInfo(USER_ADDRESS, Arg.Any<CancellationToken>())
                            .Returns(UniTask.FromResult(expectedResponse));

            // Act
            CameraReelStorageStatus result = await storageService.GetUserGalleryStorageInfo(USER_ADDRESS);

            // Assert
            Assert.That(result.ScreenshotsAmount, Is.EqualTo(expectedResponse.currentImages));
            Assert.That(result.MaxScreenshots, Is.EqualTo(expectedResponse.maxImages));
            await metadataDatabase.Received(1).GetStorageInfo(USER_ADDRESS, Arg.Any<CancellationToken>());
        }

        [Test]
        public async Task GetScreenshotGallery_ShouldReturnResponseFromDatabase()
        {
            // Arrange

            var expectedResponse = new CameraReelResponses();
            metadataDatabase.GetScreenshots(USER_ADDRESS, LIMIT, OFFSET, Arg.Any<CancellationToken>())
                            .Returns(UniTask.FromResult(expectedResponse));

            // Act
            var result = await storageService.GetScreenshotGallery(USER_ADDRESS, LIMIT, OFFSET, CancellationToken.None);

            // Assert
            Assert.That(result, Is.EqualTo(expectedResponse));
            await metadataDatabase.Received(1).GetScreenshots(USER_ADDRESS, LIMIT, OFFSET, Arg.Any<CancellationToken>());
        }

        [Test]
        public async Task DeleteScreenshot_ShouldReturnUpdatedStatus()
        {
            // Arrange
            const string UUID = "test-uuid";
            var expectedResponse = new CameraReelStorageResponse { currentImages = 4, maxImages = 10 };
            metadataDatabase.DeleteScreenshot(UUID, Arg.Any<CancellationToken>())
                            .Returns(UniTask.FromResult(expectedResponse));

            // Act
            var result = await storageService.DeleteScreenshot(UUID);

            // Assert
            Assert.That(result.ScreenshotsAmount, Is.EqualTo(expectedResponse.currentImages));
            Assert.That(result.MaxScreenshots, Is.EqualTo(expectedResponse.maxImages));
            await metadataDatabase.Received(1).DeleteScreenshot(UUID, Arg.Any<CancellationToken>());
        }
    }
}
