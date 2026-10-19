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
    public class PresentationUrlVideoShould
    {
        private const string URL = "https://www.youtube.com/watch?v=dQw4w9WgXcQ";
        private const string DIRECT_URL = "https://example.com/v.m3u8";
        private const string SIGNED_VIMEO_URL = "https://player.vimeo.com/external/123456.m3u8?s=secret-token";
        private const string POOL_CONTAINER_NAME = "POOL_CONTAINER_MEDIA_PLAYER";

        private MediaPlayer prefab = null!;
        private MediaPlayerCustomPool pool = null!;
        private IUrlResolverService resolver = null!;
        private PresentationUrlVideo video = null!;

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
            video = new PresentationUrlVideo(pool, resolver);
            MediaOpenThrottle.MarkOpened(float.PositiveInfinity);
        }

        [TearDown]
        public void TearDown()
        {
            video.Dispose();
            MediaOpenThrottle.MarkOpened(float.NegativeInfinity);
        }

        [Test]
        public void DiscardResolve_WhenReleasedBeforeItCompletes()
        {
            // Arrange
            var resolve = new UniTaskCompletionSource<ResolvedMediaUrl>();
            Resolve().Returns(resolve.Task, UniTask.FromResult(new ResolvedMediaUrl(DIRECT_URL, true)));
            video.Update(URL, null, true, 0f);

            // Act
            video.Release();
            resolve.TrySetResult(new ResolvedMediaUrl(DIRECT_URL, false));
            video.Update(URL, null, true, 1f);

            // Assert
            AssertResolved(2);
        }

        [Test]
        public void KeepUnreachableUrl_UntilReleased()
        {
            // Arrange
            Resolve().Returns(UniTask.FromResult(new ResolvedMediaUrl(DIRECT_URL, false)));
            video.Update(URL, null, true, 0f);
            video.Update(URL, null, true, 1f);
            AssertResolved(1);

            // Act
            video.Release();
            video.Update(URL, null, true, 2f);

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
            video.Update(SIGNED_VIMEO_URL, null, true, 0f);
        }

        [Test]
        public void LogUnreachableYouTubeUrl_WithItsVideoId()
        {
            // Arrange
            Resolve().Returns(UniTask.FromResult(new ResolvedMediaUrl(URL, false)));
            LogAssert.Expect(LogType.Warning, new Regex(@"watch\?v=dQw4w9WgXcQ$"));

            // Act & Assert
            video.Update(URL, null, true, 0f);
        }

        [Test]
        public void ReResolve_WhenResolvedUrlExpiresBeforeOpening()
        {
            // Arrange
            Resolve().Returns(UniTask.FromResult(new ResolvedMediaUrl(DIRECT_URL, true, expiresAtRealtimeSinceStartup: 10f)));
            video.Update(URL, null, true, 0f);

            // Act
            video.Update(URL, null, true, 11f);
            video.Update(URL, null, true, 11.1f);

            // Assert
            AssertResolved(2);
        }

        [Test]
        public void KeepResolvedUrl_BeforeItExpires()
        {
            // Arrange
            Resolve().Returns(UniTask.FromResult(new ResolvedMediaUrl(DIRECT_URL, true, expiresAtRealtimeSinceStartup: 10f)));
            video.Update(URL, null, true, 0f);

            // Act
            video.Update(URL, null, true, 5f);
            video.Update(URL, null, true, 5.1f);

            // Assert
            AssertResolved(1);
        }

        [Test]
        public void KeepResolvedUrl_WhenItNeverExpires()
        {
            // Arrange
            Resolve().Returns(UniTask.FromResult(new ResolvedMediaUrl(DIRECT_URL, true)));
            video.Update(URL, null, true, 0f);

            // Act
            video.Update(URL, null, true, 100_000f);
            video.Update(URL, null, true, 100_000.1f);

            // Assert
            AssertResolved(1);
        }

        private UniTask<ResolvedMediaUrl> Resolve() =>
            resolver.ResolveAsync(URL, Arg.Any<ReportData>(), Arg.Any<CancellationToken>());

        private void AssertResolved(int times) =>
            resolver.Received(times).ResolveAsync(URL, Arg.Any<ReportData>(), Arg.Any<CancellationToken>());
    }
}
