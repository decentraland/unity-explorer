using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UUAV;

namespace DCL.SDKComponents.MediaStream.Tests
{
    /// <summary>
    ///     Covers the DSP-callback capture AudioAnalysis depends on. In EditMode Unity never awakes the component,
    ///     so the native id stays 0 and the callback takes the silence path without touching the native library.
    /// </summary>
    public class UuavPlayerAudioCaptureShould
    {
        private const int CHANNELS = 2;

        private GameObject host = null!;
        private UUAVPlayer player = null!;
        private float[] destination = null!;

        [SetUp]
        public void SetUp()
        {
            host = new GameObject(nameof(UuavPlayerAudioCaptureShould));
            player = host.AddComponent<UUAVPlayer>();
            destination = new float[4];
        }

        [TearDown]
        public void TearDown()
        {
            // OnDestroy runs against a component whose Awake never assigned its AudioSource and logs for it,
            // an EditMode artifact rather than a failure.
            LogAssert.ignoreFailingMessages = true;
            Object.DestroyImmediate(host);
            LogAssert.ignoreFailingMessages = false;
        }

        [Test]
        public void CaptureNothingWhileDisabled()
        {
            // Act
            player.OnAudioFilterReadForTests(new float[4], CHANNELS);
            bool read = player.TryReadLastAudioFrame(destination, out _, out _);

            // Assert
            Assert.That(read, Is.False);
        }

        [Test]
        public void CaptureTheBufferAfterItWasFilled()
        {
            // Arrange: without a native player the callback silences the buffer; the capture must see that result.
            player.SetAudioCaptureEnabled(true);
            float[] dspBuffer = { 1f, 1f, 1f, 1f };

            // Act
            player.OnAudioFilterReadForTests(dspBuffer, CHANNELS);
            bool read = player.TryReadLastAudioFrame(destination, out int channels, out int sampleCount);

            // Assert
            Assert.That(read, Is.True);
            Assert.That(destination, Is.EqualTo(new[] { 0f, 0f, 0f, 0f }));
            Assert.That(channels, Is.EqualTo(CHANNELS));
            Assert.That(sampleCount, Is.EqualTo(dspBuffer.Length));
        }

        [Test]
        public void HandOverEachCapturedFrameOnce()
        {
            // Arrange
            player.SetAudioCaptureEnabled(true);
            player.OnAudioFilterReadForTests(new float[4], CHANNELS);
            player.TryReadLastAudioFrame(destination, out _, out _);

            // Act
            bool secondRead = player.TryReadLastAudioFrame(destination, out _, out _);

            // Assert
            Assert.That(secondRead, Is.False);
        }

        [Test]
        public void StopCapturingOnceDisabled()
        {
            // Arrange
            player.SetAudioCaptureEnabled(true);
            player.SetAudioCaptureEnabled(false);

            // Act
            player.OnAudioFilterReadForTests(new float[4], CHANNELS);
            bool read = player.TryReadLastAudioFrame(destination, out _, out _);

            // Assert
            Assert.That(read, Is.False);
        }

        [Test]
        public void DiscardThePendingFrameWhenDisabled()
        {
            // Arrange
            player.SetAudioCaptureEnabled(true);
            player.OnAudioFilterReadForTests(new float[4], CHANNELS);

            // Act
            player.SetAudioCaptureEnabled(false);
            bool read = player.TryReadLastAudioFrame(destination, out _, out _);

            // Assert
            Assert.That(read, Is.False);
        }
    }
}
