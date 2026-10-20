using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using NSubstitute;
using NUnit.Framework;
using System.Text.RegularExpressions;
using System.Threading;
using UnityEngine;
using UnityEngine.TestTools;
using UUAV.Compat;
using Object = UnityEngine.Object;

namespace DCL.SDKComponents.MediaStream.Tests
{
    public class PresentationUrlPlayerShould
    {
        private const string URL = "https://www.youtube.com/watch?v=dQw4w9WgXcQ";
        private const string DIRECT_URL = "https://example.com/v.m3u8";
        private const string SIGNED_VIMEO_URL = "https://player.vimeo.com/external/123456.m3u8?s=secret-token";
        private const string VIMEO_URL_MENTIONING_YOUTUBE = "https://player.vimeo.com/external/123456.m3u8?s=youtu.be/secret-token";
        private const string POOL_CONTAINER_NAME = "POOL_CONTAINER_MEDIA_PLAYER";

        private MediaPlayer prefab = null!;
        private MediaPlayerCustomPool pool = null!;
        private IUrlResolverService resolver = null!;
        private PresentationUrlPlayer urlPlayer = null!;

        [OneTimeSetUp]
        public void CreatePool()
        {
            prefab = new GameObject().AddComponent<MediaPlayer>();
            pool = new MediaPlayerCustomPool(prefab);
        }

        [OneTimeTearDown]
        public void DestroyPool()
        {
            Object.DestroyImmediate(prefab.gameObject);
            Object.DestroyImmediate(GameObject.Find(POOL_CONTAINER_NAME));
        }

        [SetUp]
        public void SetUp()
        {
            resolver = Substitute.For<IUrlResolverService>();
            urlPlayer = new PresentationUrlPlayer(pool, resolver);
        }

        [TearDown]
        public void TearDown()
        {
            urlPlayer.Dispose();
        }

        [Test]
        public void DiscardResolve_WhenReleasedBeforeItCompletes()
        {
            // Arrange
            var resolve = new UniTaskCompletionSource<ResolvedMediaUrl>();
            Resolve().Returns(resolve.Task, UniTask.FromResult(new ResolvedMediaUrl(DIRECT_URL, true)));
            urlPlayer.Update(URL, null, true, 0f);

            // Act
            urlPlayer.Release();
            resolve.TrySetResult(new ResolvedMediaUrl(DIRECT_URL, false));
            urlPlayer.Update(URL, null, true, 1f);

            // Assert
            AssertResolved(2);
        }

        [Test]
        public void KeepUnreachableUrl_UntilReleased()
        {
            // Arrange
            Resolve().Returns(UniTask.FromResult(new ResolvedMediaUrl(DIRECT_URL, false)));
            urlPlayer.Update(URL, null, true, 0f);
            urlPlayer.Update(URL, null, true, 1f);
            AssertResolved(1);

            // Act
            urlPlayer.Release();
            urlPlayer.Update(URL, null, true, 2f);

            // Assert
            AssertResolved(2);
        }

        [Test]
        public void LogUnreachableUrl_WithoutItsQuery()
        {
            // Arrange
            resolver.ResolveAsync(SIGNED_VIMEO_URL, Arg.Any<ReportData>(), Arg.Any<CancellationToken>())
                    .Returns(UniTask.FromResult(new ResolvedMediaUrl(SIGNED_VIMEO_URL, false)));
            LogAssert.Expect(LogType.Warning, new Regex(@"external/123456\.m3u8$"));

            // Act & Assert
            urlPlayer.Update(SIGNED_VIMEO_URL, null, true, 0f);
        }

        [Test]
        public void LogUnreachableVimeoUrl_WithoutItsQuery_WhenTheQueryMentionsYouTube()
        {
            // Arrange
            resolver.ResolveAsync(VIMEO_URL_MENTIONING_YOUTUBE, Arg.Any<ReportData>(), Arg.Any<CancellationToken>())
                    .Returns(UniTask.FromResult(new ResolvedMediaUrl(VIMEO_URL_MENTIONING_YOUTUBE, false)));
            LogAssert.Expect(LogType.Warning, new Regex(@"external/123456\.m3u8$"));

            // Act & Assert
            urlPlayer.Update(VIMEO_URL_MENTIONING_YOUTUBE, null, true, 0f);
        }

        [Test]
        public void LogUnreachableYouTubeUrl_WithItsVideoId()
        {
            // Arrange
            Resolve().Returns(UniTask.FromResult(new ResolvedMediaUrl(URL, false)));
            LogAssert.Expect(LogType.Warning, new Regex(@"watch\?v=dQw4w9WgXcQ$"));

            // Act & Assert
            urlPlayer.Update(URL, null, true, 0f);
        }

        [Test]
        public void ReResolve_WhenResolvedUrlExpiresBeforeOpening()
        {
            // Arrange
            Resolve().Returns(UniTask.FromResult(new ResolvedMediaUrl(DIRECT_URL, true, expiresAtRealtimeSinceStartup: 10f)));
            urlPlayer.Update(URL, null, true, 0f);

            // Act
            urlPlayer.Update(URL, null, true, 11f);
            urlPlayer.Update(URL, null, true, 11.1f);

            // Assert
            AssertResolved(2);
        }

        [TestCase(0f, 1e6f, false)]
        [TestCase(10f, 9.9f, false)]
        [TestCase(10f, 10f, true)]
        public void DetectExpiry(float expiresAt, float now, bool expected)
        {
            // Act
            bool expired = PresentationUrlPlayer.IsExpired(expiresAt, now);

            // Assert
            Assert.AreEqual(expected, expired);
        }

        private UniTask<ResolvedMediaUrl> Resolve() =>
            resolver.ResolveAsync(URL, Arg.Any<ReportData>(), Arg.Any<CancellationToken>());

        private void AssertResolved(int times) =>
            resolver.Received(times).ResolveAsync(URL, Arg.Any<ReportData>(), Arg.Any<CancellationToken>());
    }
}
