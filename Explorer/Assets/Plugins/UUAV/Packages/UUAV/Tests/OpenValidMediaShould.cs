using System.Collections;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace UUAV.Tests
{
    public class OpenValidMediaShould : UUAVTestBase
    {
        [UnityTest]
        public IEnumerator ReachReadyWithCorrectMediaInfo()
        {
            // Arrange
            UUAVPlayer player = CreatePlayer(out _);

            // Act
            yield return OpenAndAwaitReady(player, UrlFor(Fixtures.ToneColorBands));

            // Assert
            Assert.That(player.TryGetMediaInfo(out MediaInfo info), Is.True, Wait.Diagnostics(player));
            Assert.That(info.HasVideo, Is.True);
            Assert.That(info.HasAudio, Is.True);
            Assert.That(info.Duration, Is.EqualTo(Fixtures.DurationSeconds).Within(0.5));
            Assert.That((int)info.Width, Is.EqualTo(Fixtures.Width));
            Assert.That((int)info.Height, Is.EqualTo(Fixtures.Height));
            Assert.That(info.VideoCodec, Is.EqualTo(Fixtures.VideoCodec));
            Assert.That(info.AudioCodec, Is.EqualTo(Fixtures.AudioCodec));
        }

        [UnityTest]
        public IEnumerator ReportAudioOnlyStreams()
        {
            // Arrange
            UUAVPlayer player = CreatePlayer(out _);

            // Act
            yield return OpenAndAwaitReady(player, UrlFor(Fixtures.AudioOnly));

            // Assert
            Assert.That(player.TryGetMediaInfo(out MediaInfo info), Is.True, Wait.Diagnostics(player));
            Assert.That(info.HasAudio, Is.True);
            Assert.That(info.HasVideo, Is.False);
            Assert.That(info.AudioCodec, Is.EqualTo(Fixtures.AudioCodec));
            Assert.That(info.Duration, Is.EqualTo(Fixtures.DurationSeconds).Within(0.5));
        }

        [UnityTest]
        public IEnumerator ReportVideoOnlyStreams()
        {
            // Arrange
            UUAVPlayer player = CreatePlayer(out _);

            // Act
            yield return OpenAndAwaitReady(player, UrlFor(Fixtures.VideoOnly));

            // Assert
            Assert.That(player.TryGetMediaInfo(out MediaInfo info), Is.True, Wait.Diagnostics(player));
            Assert.That(info.HasVideo, Is.True);
            Assert.That(info.HasAudio, Is.False);
            Assert.That(info.VideoCodec, Is.EqualTo(Fixtures.VideoCodec));
        }

        [UnityTest]
        public IEnumerator UpdateMediaInfoOnReopen()
        {
            // Arrange
            UUAVPlayer player = CreatePlayer(out _);
            yield return OpenAndAwaitReady(player, UrlFor(Fixtures.ToneColorBands));

            // Act: reopen a different url on the same player
            player.OpenMedia(UrlFor(Fixtures.VideoOnly));

            // Assert: poll the info itself instead of the state, because the
            // async reopen can leave the previous Ready state (and the
            // previous media info) visible for a few frames; the Ready gate
            // keeps TryGetMediaInfo from erroring mid-Opening
            yield return Wait.Until(
                () => player.State == UUAVState.Ready
                      && player.TryGetMediaInfo(out MediaInfo current)
                      && current.HasAudio == false,
                OpenTimeout,
                () => $"media info still describes the previous url\n{Wait.Diagnostics(player)}"
            );
            Assert.That(player.CurrentUrl, Is.EqualTo(UrlFor(Fixtures.VideoOnly)));
        }

        [UnityTest]
        public IEnumerator ReturnToClosedOnCloseAndStayReusable()
        {
            // Arrange
            UUAVPlayer player = CreatePlayer(out _);
            yield return OpenAndAwaitReady(player, UrlFor(Fixtures.ToneColorBands));

            // Act
            player.CloseMedia();
            yield return Wait.ForState(player, UUAVState.Closed, StateTimeout, "after CloseMedia");

            // Assert: the closed player accepts a fresh open
            yield return OpenAndAwaitReady(player, UrlFor(Fixtures.ToneColorBands));
        }
    }
}
