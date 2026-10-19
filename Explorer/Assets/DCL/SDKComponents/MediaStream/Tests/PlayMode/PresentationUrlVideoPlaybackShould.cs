#if UNITY_EDITOR
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using NSubstitute;
using NUnit.Framework;
using System;
using System.Collections;
using System.Text.RegularExpressions;
using System.Threading;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UUAV.Compat;
using UUAV.Tests;
using Object = UnityEngine.Object;

namespace DCL.SDKComponents.MediaStream.Tests
{
    public class PresentationUrlVideoPlaybackShould : UUAVTestBase
    {
        private const string URL = "https://www.youtube.com/watch?v=dQw4w9WgXcQ";
        private const string MEDIA_PLAYER_PREFAB_PATH = "Assets/DCL/Audio/Prefabs/MediaPlayer.prefab";
        private const string POOL_CONTAINER_NAME = "POOL_CONTAINER_MEDIA_PLAYER";
        private const float TIMEOUT_SECONDS = 30f;

        private MediaPlayerCustomPool pool = null!;
        private IUrlResolverService resolver = null!;
        private PresentationUrlVideo video = null!;
        private float now;

        private MediaPlayer? mediaPlayer =>
            GameObject.Find(POOL_CONTAINER_NAME) is { } container ? container.GetComponentInChildren<MediaPlayer>() : null;

        [SetUp]
        public void SetUp()
        {
            pool = new MediaPlayerCustomPool(AssetDatabase.LoadAssetAtPath<MediaPlayer>(MEDIA_PLAYER_PREFAB_PATH));
            resolver = Substitute.For<IUrlResolverService>();
            video = new PresentationUrlVideo(pool, resolver);
            MediaOpenThrottle.MarkOpened(float.NegativeInfinity);
            now = 0f;
        }

        [TearDown]
        public void TearDown()
        {
            video.Dispose();
            Object.Destroy(GameObject.Find(POOL_CONTAINER_NAME));
            MediaOpenThrottle.MarkOpened(float.NegativeInfinity);
        }

        [UnityTest]
        public IEnumerator PlayResolvedUrl_OnceItOpens()
        {
            // Arrange
            ResolveTo(Fixtures.ToneColorBands);

            // Act
            yield return UpdateUntil(() => mediaPlayer is { } player && player.Control.IsPlaying() && video.Texture != null, null, true, "playing");

            // Assert
            Assert.IsNotNull(video.Texture);
            Assert.IsTrue(video.RequiresCompositorFlip);
        }

        [UnityTest]
        public IEnumerator FollowPresenter_WhenItPausesAndResumes()
        {
            // Arrange
            ResolveTo(Fixtures.ToneColorBands);
            yield return UpdateUntil(() => mediaPlayer is { } player && player.Control.IsPlaying(), null, true, "playing");

            // Act
            yield return UpdateUntil(() => mediaPlayer!.Control.IsPaused(), null, false, "paused");
            yield return UpdateUntil(() => mediaPlayer!.Control.IsPlaying(), null, true, "resumed");

            // Assert
            Assert.IsTrue(mediaPlayer!.Control.IsPlaying());
        }

        [UnityTest]
        public IEnumerator SeekToTarget_WhenItDriftsAndHoldTheNextSeek()
        {
            // Arrange
            ResolveTo(Fixtures.ToneColorBands);
            yield return UpdateUntil(() => mediaPlayer is { } player && player.Control.IsPlaying(), null, true, "playing");
            yield return UpdateUntil(() => mediaPlayer!.Control.GetCurrentTime() >= 3.5, 4.0, true, "seek to 4 s");

            // Act
            yield return UpdateFor(1f, 0.5, true);

            // Assert
            Assert.GreaterOrEqual(mediaPlayer!.Control.GetCurrentTime(), 3.5);
        }

        [UnityTest]
        public IEnumerator HoldTheEnd_UntilTargetRewinds()
        {
            // Arrange
            ResolveTo(Fixtures.ToneColorBands);
            yield return UpdateUntil(() => mediaPlayer is { } player && player.Control.IsPlaying(), null, true, "playing");
            yield return UpdateUntil(() => mediaPlayer!.Control.IsFinished(), 5.0, true, "finished");

            // Act
            yield return UpdateFor(1f, 5.5, true);
            bool heldTheEnd = mediaPlayer!.Control.IsFinished();
            yield return UpdateUntil(() => mediaPlayer!.Control.IsPlaying() && mediaPlayer.Control.GetCurrentTime() < 3.0, 1.0, true, "rewound");

            // Assert
            Assert.IsTrue(heldTheEnd);
        }

        [UnityTest]
        public IEnumerator KeepFailedUrl_WhenMediaErrors()
        {
            // Arrange
            ResolveTo(Fixtures.Garbage);
            LogAssert.Expect(LogType.Warning, new Regex("could not be played"));
            yield return UpdateUntil(() => mediaPlayer != null, null, true, "opened");

            // Act
            yield return UpdateUntil(() => mediaPlayer == null, null, true, "released");
            yield return UpdateFor(1f, null, true);

            // Assert
            Assert.IsNull(mediaPlayer);
            AssertResolved(1);
        }

        [UnityTest]
        public IEnumerator ReResolve_WhenMediaErrorsAfterResolvedUrlExpires()
        {
            // Arrange
            resolver.ResolveAsync(URL, Arg.Any<ReportData>(), Arg.Any<CancellationToken>())
                    .Returns(UniTask.FromResult(new ResolvedMediaUrl(UrlFor(Fixtures.Garbage), true, expiresAtRealtimeSinceStartup: 1000f)));

            yield return UpdateUntil(() => mediaPlayer != null, null, true, "opened");
            yield return WaitUntil(() => mediaPlayer!.Control.GetLastError() != ErrorCode.None, "media error");

            // Act
            now = 2000f;
            video.Update(URL, null, true, now);
            video.Update(URL, null, true, now);

            // Assert
            AssertResolved(2);
        }

        [UnityTest]
        public IEnumerator DestroyPlayer_WhenPausedBeforeMediaOpens()
        {
            // Arrange
            ResolveTo(Fixtures.ToneColorBands);
            Tick(null, true);
            Tick(null, true);
            Assert.IsNotNull(mediaPlayer);

            // Act
            video.Pause();
            yield return null;
            bool destroyed = mediaPlayer == null;
            yield return UpdateUntil(() => mediaPlayer != null, null, true, "reopened");

            // Assert
            Assert.IsTrue(destroyed);
        }

        private void ResolveTo(string fixture) =>
            resolver.ResolveAsync(URL, Arg.Any<ReportData>(), Arg.Any<CancellationToken>())
                    .Returns(UniTask.FromResult(new ResolvedMediaUrl(UrlFor(fixture), true)));

        private void AssertResolved(int times) =>
            resolver.Received(times).ResolveAsync(URL, Arg.Any<ReportData>(), Arg.Any<CancellationToken>());

        private IEnumerator UpdateUntil(Func<bool> condition, double? targetSeconds, bool playing, string expected)
        {
            float deadline = UnityEngine.Time.realtimeSinceStartup + TIMEOUT_SECONDS;

            while (!condition())
            {
                Assert.Less(UnityEngine.Time.realtimeSinceStartup, deadline, $"timed out waiting for: {expected}");
                Tick(targetSeconds, playing);
                yield return null;
            }
        }

        private IEnumerator UpdateFor(float seconds, double? targetSeconds, bool playing)
        {
            float end = UnityEngine.Time.realtimeSinceStartup + seconds;

            while (UnityEngine.Time.realtimeSinceStartup < end)
            {
                Tick(targetSeconds, playing);
                yield return null;
            }
        }

        private static IEnumerator WaitUntil(Func<bool> condition, string expected)
        {
            float deadline = UnityEngine.Time.realtimeSinceStartup + TIMEOUT_SECONDS;

            while (!condition())
            {
                Assert.Less(UnityEngine.Time.realtimeSinceStartup, deadline, $"timed out waiting for: {expected}");
                yield return null;
            }
        }

        private void Tick(double? targetSeconds, bool playing)
        {
            now += UnityEngine.Time.unscaledDeltaTime;
            video.Update(URL, targetSeconds, playing, now);

            if (mediaPlayer is { } player && !player.GetComponent<AudioTapBehaviour>())
                player.gameObject.AddComponent<AudioTapBehaviour>();
        }
    }
}
#endif
