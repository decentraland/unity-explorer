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

        [TestCase(30.0, 10.0, false, true)]
        [TestCase(30.0, 29.0, false, false)]
        [TestCase(30.0, 40.0, false, false)]
        [TestCase(30.0, 10.0, true, false)]
        public void DetectRewind_OnlyWhenTargetIsMoreThanTwoSecondsBehind(double currentSeconds, double targetSeconds, bool isLive, bool expected)
        {
            // Act
            bool rewind = PresentationUrlPlayer.IsRewind(currentSeconds, targetSeconds, isLive);

            // Assert
            Assert.AreEqual(expected, rewind);
        }

        [TestCase(3f, false, false, true)]
        [TestCase(2.9f, false, false, false)]
        [TestCase(3f, true, false, false)]
        [TestCase(3f, false, true, false)]
        public void Seek_AtMostEveryThreeSeconds_WhenIdleAndOnDemand(float secondsSinceLastSeek, bool isBusy, bool isLive, bool expected)
        {
            // Act
            bool seek = PresentationUrlPlayer.ShouldSeek(10.0, 20.0, isLive, isBusy, 100f, 100f - secondsSinceLastSeek);

            // Assert
            Assert.AreEqual(expected, seek);
        }

        [Test]
        public void SkipSeek_WhenDriftIsWithinThreshold()
        {
            // Act
            bool seek = PresentationUrlPlayer.ShouldSeek(10.0, 11.0, false, false, 100f, float.NegativeInfinity);

            // Assert
            Assert.IsFalse(seek);
        }

        [Test]
        public void DetectNoRewind_WithoutTarget()
        {
            // Act
            bool rewind = PresentationUrlPlayer.IsRewind(30.0, null, false);

            // Assert
            Assert.IsFalse(rewind);
        }

        [TestCase(10.0, 11.9)]
        [TestCase(11.9, 10.0)]
        public void SkipSeek_WhenDriftIsWithinTwoSeconds(double currentSeconds, double targetSeconds)
        {
            // Act & Assert
            Assert.IsFalse(PresentationUrlPlayer.NeedsSeek(currentSeconds, targetSeconds, false));
        }

        [TestCase(10.0, 12.1)]
        [TestCase(12.1, 10.0)]
        public void RequireSeek_WhenDriftExceedsTwoSeconds(double currentSeconds, double targetSeconds)
        {
            // Act & Assert
            Assert.IsTrue(PresentationUrlPlayer.NeedsSeek(currentSeconds, targetSeconds, false));
        }

        [Test]
        public void ReturnFalse_WhenLive()
        {
            // Act & Assert
            Assert.IsFalse(PresentationUrlPlayer.NeedsSeek(0, 100, true));
        }

        [Test]
        public void ReturnFalse_WhenTargetIsNull()
        {
            // Act & Assert
            Assert.IsFalse(PresentationUrlPlayer.NeedsSeek(0, null, false));
        }

        private UniTask<ResolvedMediaUrl> Resolve() =>
            resolver.ResolveAsync(URL, Arg.Any<ReportData>(), Arg.Any<CancellationToken>());

        private void AssertResolved(int times) =>
            resolver.Received(times).ResolveAsync(URL, Arg.Any<ReportData>(), Arg.Any<CancellationToken>());
    }
}
