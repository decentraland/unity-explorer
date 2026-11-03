using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UUAV;
using UUAV.Compat;

namespace DCL.SDKComponents.MediaStream.Tests
{
    /// <summary>
    ///     Covers the AudioAnalysis capture lifecycle on a URL player: capture is enabled on the player's DSP
    ///     callback and disabled before the pooled player can be handed to another entity.
    /// </summary>
    public class MediaPlayerComponentAudioFrameBufferShould
    {
        private const int CHANNELS = 2;

        private GameObject host = null!;
        private MediaPlayer mediaPlayer = null!;
        private UUAVPlayer uuavPlayer = null!;
        private MediaPlayerComponent component;
        private float[] destination = null!;

        [SetUp]
        public void SetUp()
        {
            host = new GameObject(nameof(MediaPlayerComponentAudioFrameBufferShould));
            mediaPlayer = host.AddComponent<MediaPlayer>();

            // EditMode never awakes components: run the facade's Awake by hand so it owns a UUAVPlayer.
            // That player is not awoken either, so its native id stays 0 and no native call is made.
            mediaPlayer.AwakeForTests();
            uuavPlayer = host.GetComponent<UUAVPlayer>();

            component = new MediaPlayerComponent(MultiMediaPlayer.FromUrlMediaPlayer(new UrlMediaPlayer(mediaPlayer, null!)), isFromContentServer: false);
            destination = new float[4];
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
        public void ConsumeNothingBeforeAttach()
        {
            // Act
            uuavPlayer.OnAudioFilterReadForTests(new float[4], CHANNELS);
            bool consumed = component.TryConsumeAudioFrame(destination, out _, out _);

            // Assert
            Assert.That(consumed, Is.False);
        }

        [Test]
        public void EnableCaptureOnThePlayerWhenAttached()
        {
            // Act
            bool attached = component.TryAttachAudioFrameBuffer();
            uuavPlayer.OnAudioFilterReadForTests(new float[4], CHANNELS);
            bool consumed = component.TryConsumeAudioFrame(destination, out _, out _);

            // Assert
            Assert.That(attached, Is.True);
            Assert.That(consumed, Is.True);
        }

        [Test]
        public void ConsumeTheCapturedFrame()
        {
            // Arrange: without a native player the callback silences the buffer; the frame must carry that result.
            component.TryAttachAudioFrameBuffer();
            float[] dspBuffer = { 0.5f, 0.5f, 0.5f, 0.5f };

            // Act
            uuavPlayer.OnAudioFilterReadForTests(dspBuffer, CHANNELS);
            bool consumed = component.TryConsumeAudioFrame(destination, out int channels, out int sampleCount);

            // Assert
            Assert.That(consumed, Is.True);
            Assert.That(destination, Is.EqualTo(new[] { 0f, 0f, 0f, 0f }));
            Assert.That(channels, Is.EqualTo(CHANNELS));
            Assert.That(sampleCount, Is.EqualTo(dspBuffer.Length));
        }

        [Test]
        public void KeepCapturingOnRepeatedAttach()
        {
            // Arrange
            component.TryAttachAudioFrameBuffer();

            // Act
            component.TryAttachAudioFrameBuffer();
            uuavPlayer.OnAudioFilterReadForTests(new float[4], CHANNELS);
            bool consumed = component.TryConsumeAudioFrame(destination, out _, out _);

            // Assert
            Assert.That(consumed, Is.True);
        }

        [Test]
        public void DisableCaptureOnThePlayerWhenDetached()
        {
            // Arrange
            component.TryAttachAudioFrameBuffer();

            // Act
            component.DetachAudioFrameBuffer();
            uuavPlayer.OnAudioFilterReadForTests(new float[4], CHANNELS);
            bool consumed = component.TryConsumeAudioFrame(destination, out _, out _);

            // Assert
            Assert.That(consumed, Is.False);
        }
    }
}
