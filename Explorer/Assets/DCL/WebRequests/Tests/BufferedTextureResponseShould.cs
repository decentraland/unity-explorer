using Cysharp.Threading.Tasks;
using DCL.Multiplayer.Connections.DecentralandUrls;
using NSubstitute;
using NUnit.Framework;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;
using Object = UnityEngine.Object;

namespace DCL.WebRequests.Tests
{
    public class BufferedTextureResponseShould
    {
        // Both images contain an opaque red top pixel and a translucent green bottom pixel.
        private const string WEBP = "UklGRiAAAABXRUJQVlA4TBMAAAAvAEAAEA8w//sfB/wPBxMQ0f8QAA==";
        private const string PNG = "iVBORw0KGgoAAAANSUhEUgAAAAEAAAACCAYAAACZgbYnAAAADklEQVR4nGP4z8AARvUAEncDfUnFjVsAAAAASUVORK5CYII=";

        private string responsePath = null!;
        private Texture2D? texture;

        [SetUp]
        public void SetUp()
        {
            responsePath = Path.Combine(Application.dataPath, "../Temp", Guid.NewGuid().ToString("N"));
        }

        [TearDown]
        public void TearDown()
        {
            if (texture != null)
                Object.DestroyImmediate(texture);

            texture = null;
            File.Delete(responsePath);
        }

        [TestCase(WEBP, TestName = "DecodeColdWebPPosterWithoutAFileExtension")]
        [TestCase(PNG, TestName = "DecodePngPosterWithoutAFileExtension")]
        public async Task DecodePoster(string encoded)
        {
            texture = await DecodeAsync(Convert.FromBase64String(encoded));

            Assert.That(texture, Is.Not.Null);
            Assert.AreEqual(1, texture!.width);
            Assert.AreEqual(2, texture.height);
            CollectionAssert.AreEqual(new[] { new Color32(0, 255, 0, 127), new Color32(255, 0, 0, 255) }, texture.GetPixels32());
            Assert.AreEqual(TextureWrapMode.Clamp, texture.wrapMode);
            Assert.AreEqual(FilterMode.Bilinear, texture.filterMode);
        }

        [TestCase("UklGRiAAAABXRUJQ", TestName = "RejectTruncatedWebP")]
        [TestCase("UklGRiAAAABXRUJQVlA4TBMAAAAvAEAAEA8w//sfB/w=", TestName = "RejectTruncatedWebPBitstreamWithValidDimensions")]
        [TestCase("R0lGODlhAQABAIAAAAAAAP///yH5BAEAAAAALAAAAAABAAEAAAIBRAA7", TestName = "RejectUnsupportedGif")]
        public async Task RejectInvalidPoster(string encoded)
        {
            InvalidDataException? failure = null;

            try { texture = await DecodeAsync(Convert.FromBase64String(encoded)); }
            catch (InvalidDataException e) { failure = e; }

            Assert.That(failure, Is.Not.Null);
            Assert.That(texture, Is.Null);
        }

        private async Task<Texture2D?> DecodeAsync(byte[] data)
        {
            File.WriteAllBytes(responsePath, data);
            GetTextureWebRequest request = GetTextureWebRequest.Initialize(new Uri(responsePath).AbsoluteUri,
                new GetTextureArguments(TextureType.Albedo, useKtx: false), Substitute.For<IDecentralandUrlsSource>(), false);
            using UnityWebRequest download = request.UnityWebRequest;
            await download.SendWebRequest();
            return await GetTextureWebRequest.CreateTexture(TextureWrapMode.Clamp, FilterMode.Bilinear).ExecuteAsync(request, CancellationToken.None);
        }
    }
}
