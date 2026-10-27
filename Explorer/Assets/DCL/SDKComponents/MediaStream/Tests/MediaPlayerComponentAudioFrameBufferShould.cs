using NUnit.Framework;
using Plugins.NativeAudioAnalysis;
using UnityEngine;
using UnityEngine.TestTools;
using UUAV.Compat;

namespace DCL.SDKComponents.MediaStream.Tests
{
    /// <summary>
    ///     Covers the AudioAnalysis buffer lifecycle on a URL player: the tap is registered on the player's DSP
    ///     callback and released before the pooled player can be handed to another entity.
    /// </summary>
    public class MediaPlayerComponentAudioFrameBufferShould
    {
        private const int CHANNELS = 2;

        private GameObject host = null!;
        private MediaPlayer mediaPlayer = null!;
        private MediaPlayerComponent component;

        [SetUp]
        public void SetUp()
        {
            host = new GameObject(nameof(MediaPlayerComponentAudioFrameBufferShould));
            mediaPlayer = host.AddComponent<MediaPlayer>();

            // EditMode never awakes components: run the facade's Awake by hand so it owns a UUAVPlayer.
            // That player is not awoken either, so its native id stays 0 and no native call is made.
            mediaPlayer.Awake();

            component = new MediaPlayerComponent(MultiMediaPlayer.FromUrlMediaPlayer(new UrlMediaPlayer(mediaPlayer, null!)), isFromContentServer: false);
        }

        [TearDown]
        public void TearDown()
        {
            // UUAVPlayer.OnDestroy runs against a component whose Awake never assigned its AudioSource and logs for it,
            // an EditMode artifact rather than a failure.
            LogAssert.ignoreFailingMessages = true;
            Object.DestroyImmediate(host);
            LogAssert.ignoreFailingMessages = false;
        }

        [Test]
        public void RegisterTheBufferAsThePlayerTap()
        {
            // Act
            bool attached = component.TryGetOrAttachAudioFrameBuffer(out LastAudioFrameBuffer? buffer);

            // Assert
            Assert.That(attached, Is.True);
            Assert.That(mediaPlayer.AudioFrameTap, Is.Not.Null);
            Assert.That(buffer!.SampleRate, Is.EqualTo(AudioSettings.outputSampleRate));
        }

        [Test]
        public void FeedTheBufferFromTheTap()
        {
            // Arrange
            component.TryGetOrAttachAudioFrameBuffer(out LastAudioFrameBuffer? buffer);
            float[] dspBuffer = { 0.5f, 0.5f, 0.5f, 0.5f };

            // Act
            mediaPlayer.AudioFrameTap!(dspBuffer, CHANNELS);
            bool consumed = buffer!.TryConsume(out float[]? output, out int channels, out _);

            // Assert
            Assert.That(consumed, Is.True);
            Assert.That(output, Is.EqualTo(dspBuffer));
            Assert.That(channels, Is.EqualTo(CHANNELS));
        }

        [Test]
        public void ReuseTheSameBufferOnRepeatedAttach()
        {
            // Arrange
            component.TryGetOrAttachAudioFrameBuffer(out LastAudioFrameBuffer? first);

            // Act
            component.TryGetOrAttachAudioFrameBuffer(out LastAudioFrameBuffer? second);

            // Assert
            Assert.That(second, Is.SameAs(first));
        }

        [Test]
        public void ClearThePlayerTapWhenRemoved()
        {
            // Arrange
            component.TryGetOrAttachAudioFrameBuffer(out _);

            // Act
            component.EnsureAudioFrameBufferIsRemoved();

            // Assert
            Assert.That(mediaPlayer.AudioFrameTap, Is.Null);
        }
    }
}
