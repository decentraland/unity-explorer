using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UUAV;

namespace DCL.SDKComponents.MediaStream.Tests
{
    /// <summary>
    ///     Covers the DSP-callback tap AudioAnalysis depends on. In EditMode Unity never awakes the component,
    ///     so the native id stays 0 and the callback takes the silence path without touching the native library.
    /// </summary>
    public class UuavPlayerAudioFrameTapShould
    {
        private const int CHANNELS = 2;

        private GameObject host = null!;
        private UUAVPlayer player = null!;

        [SetUp]
        public void SetUp()
        {
            host = new GameObject(nameof(UuavPlayerAudioFrameTapShould));
            player = host.AddComponent<UUAVPlayer>();
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
        public void ForwardTheDspBufferToTheTap()
        {
            // Arrange
            float[]? received = null;
            var receivedChannels = 0;

            player.AudioFrameTap = (data, channels) =>
            {
                received = data;
                receivedChannels = channels;
            };

            float[] dspBuffer = { 1f, 1f, 1f, 1f };

            // Act
            player.OnAudioFilterRead(dspBuffer, CHANNELS);

            // Assert
            Assert.That(received, Is.SameAs(dspBuffer));
            Assert.That(receivedChannels, Is.EqualTo(CHANNELS));
        }

        [Test]
        public void ForwardTheBufferAfterItWasFilled()
        {
            // Arrange: without a native player the callback silences the buffer; the tap must see that result.
            float[]? received = null;
            player.AudioFrameTap = (data, _) => received = (float[])data.Clone();
            float[] dspBuffer = { 1f, 1f, 1f, 1f };

            // Act
            player.OnAudioFilterRead(dspBuffer, CHANNELS);

            // Assert
            Assert.That(received, Is.EqualTo(new[] { 0f, 0f, 0f, 0f }));
        }

        [Test]
        public void StopForwardingOnceTheTapIsCleared()
        {
            // Arrange
            var invoked = false;
            player.AudioFrameTap = (_, _) => invoked = true;
            player.AudioFrameTap = null;

            // Act
            player.OnAudioFilterRead(new float[4], CHANNELS);

            // Assert
            Assert.That(invoked, Is.False);
        }
    }
}
