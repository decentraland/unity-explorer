using DCL.Multiplayer.Connections.DecentralandUrls;
using NSubstitute;
using NUnit.Framework;
using UnityEngine.Networking;

namespace DCL.WebRequests.Tests
{
    public class GetTextureWebRequestShould
    {
        private const string URL = "https://example.com/a.png";

        [Test]
        public void SetRedirectLimitZero_WhenRedirectsAreDisabled()
        {
            var arguments = new GetTextureArguments(TextureType.Albedo, useKtx: false, disableRedirects: true);

            using UnityWebRequest request = GetTextureWebRequest.Initialize(URL, arguments, Substitute.For<IDecentralandUrlsSource>(), false).UnityWebRequest;

            Assert.AreEqual(0, request.redirectLimit);
        }

        [Test]
        public void KeepDefaultRedirectLimit_WhenRedirectsAreNotDisabled()
        {
            var arguments = new GetTextureArguments(TextureType.Albedo, useKtx: false);

            using UnityWebRequest request = GetTextureWebRequest.Initialize(URL, arguments, Substitute.For<IDecentralandUrlsSource>(), false).UnityWebRequest;

            Assert.Greater(request.redirectLimit, 0);
        }

        [Test]
        public void DecodeNonReadableTextureOffMainThread_WhenRequested()
        {
            // Arrange
            var arguments = new GetTextureArguments(TextureType.Albedo, useKtx: false, decodeOffMainThread: true);

            // Act
            using UnityWebRequest request = GetTextureWebRequest.Initialize(URL, arguments, Substitute.For<IDecentralandUrlsSource>(), false).UnityWebRequest;

            // Assert
            Assert.IsInstanceOf<DownloadHandlerTexture>(request.downloadHandler);
        }

        [Test]
        public void BufferBody_WhenOffMainThreadDecodeIsNotRequested()
        {
            // Arrange
            var arguments = new GetTextureArguments(TextureType.Albedo, useKtx: false);

            // Act
            using UnityWebRequest request = GetTextureWebRequest.Initialize(URL, arguments, Substitute.For<IDecentralandUrlsSource>(), false).UnityWebRequest;

            // Assert
            Assert.IsInstanceOf<DownloadHandlerBuffer>(request.downloadHandler);
        }
    }
}
